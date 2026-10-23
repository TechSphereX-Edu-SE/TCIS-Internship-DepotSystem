using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    One version of the repair estimate of a case (feedback 4: chi phí sửa chữa).
    - Version 1, 2, ... per case; a version is never overwritten once submitted, so every proposal and
      decision stays in the history. At most one version is open (Draft or Submitted) at a time.
    - Draft: items can change. Submitted: locked, waiting for the decision of the approving party.
    - Decision: Approved / Rejected / ChangesRequested, with the approving party, the person, the time and
      the approval document reference. Rejected and ChangesRequested keep the case waiting; the next
      version starts as a copy of this one.
    - Approving the version is what moves the case from "Chờ duyệt dự toán" to "Đã duyệt sửa"
      (RepairCase.ApproveEstimate); later versions (extra damage found while repairing) only add cost.
    Nothing here sends e-mails, invoices or collects money, and no payment is required before repair or
    release (no such policy yet).
*/
public sealed class RepairEstimate : AuditableEntity
{
    public const int MaxLines = 50;

    public Guid RepairCaseId { get; private set; }
    public int Version { get; private set; }
    public string Currency { get; private set; } = default!;
    public RepairEstimateStatus Status { get; private set; }

    // General note on the repair, and the evidence it is based on (EIR number, photos, inspection conclusion)
    public string? Note { get; private set; }
    public string? Evidence { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? SubmittedBy { get; private set; }

    // Decision: who approved (party and person), the document, and who recorded it in the system
    public DateTimeOffset? DecidedAt { get; private set; }
    public string? DecisionRecordedBy { get; private set; }
    public string? ApproverParty { get; private set; }
    public string? ApproverName { get; private set; }
    public string? ApprovalReference { get; private set; }
    public string? DecisionNote { get; private set; }

    // PostgreSQL xmin: two decisions on the same version cannot both win
    public uint RowVersion { get; private set; }

    private readonly List<RepairEstimateLine> _lines = [];
    public IReadOnlyCollection<RepairEstimateLine> Lines => _lines.AsReadOnly();

    public decimal Total => _lines.Sum(l => l.Amount);
    public bool IsOpen => Status is RepairEstimateStatus.Draft or RepairEstimateStatus.Submitted;

    private RepairEstimate() { }

    /*
        A new version of the case estimate. Allowed once the inspection said the container needs repair
        and while the case is open. The previous version (if any) must be decided; its items are copied
        so the new version only changes what the decision asked for.
    */
    public static Result<RepairEstimate> Create(
        RepairCase repairCase, RepairEstimate? previous, string currency, string? note, string? evidence)
    {
        if (!repairCase.IsOpen)
            return Result.Failure<RepairEstimate>(RepairCase.Closed());
        if (repairCase.RepairNeeded != true)
            return Result.Failure<RepairEstimate>(Error.Conflict("RepairEstimate.NotInspected",
                "An estimate can only be made after the inspection concluded that a repair is needed."));
        if (previous is { IsOpen: true })
            return Result.Failure<RepairEstimate>(Error.Conflict("RepairEstimate.OpenVersion",
                $"Version {previous.Version} is still {previous.Status}; finish it before making a new one."));

        var estimate = new RepairEstimate
        {
            RepairCaseId = repairCase.Id,
            Version = (previous?.Version ?? 0) + 1,
            Currency = NormalizeCurrency(currency),
            Status = RepairEstimateStatus.Draft,
            Note = Clean(note) ?? previous?.Note,
            Evidence = Clean(evidence) ?? previous?.Evidence
        };
        if (previous is not null)
            estimate._lines.AddRange(previous._lines.OrderBy(l => l.Position).Select(l => l.CopyTo(estimate.Id)));
        return Result.Success(estimate);
    }

    // Replaces the header and every item of a draft (the screen edits the whole table at once)
    public Result UpdateDraft(
        string currency, string? note, string? evidence,
        IReadOnlyList<(Guid? FindingId, string Damage, string Method, decimal Amount, CostPayer Payer, string? PayerName)> lines)
    {
        if (Status != RepairEstimateStatus.Draft)
            return Result.Failure(NotDraft());
        if (lines.Count > MaxLines)
            return Result.Failure(Error.Validation("RepairEstimate.TooManyLines", $"At most {MaxLines} items per estimate."));

        var created = new List<RepairEstimateLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var line = RepairEstimateLine.Create(Id, i + 1, l.FindingId, l.Damage, l.Method, l.Amount, l.Payer, l.PayerName);
            if (line.IsFailure)
                return Result.Failure(line.Error!);
            created.Add(line.Value!);
        }

        Currency = NormalizeCurrency(currency);
        Note = Clean(note);
        Evidence = Clean(evidence);
        _lines.Clear();
        _lines.AddRange(created);
        return Result.Success();
    }

    // Sends the draft for approval; an empty estimate has nothing to approve
    public Result Submit(DateTimeOffset at, string? actor)
    {
        if (Status != RepairEstimateStatus.Draft)
            return Result.Failure(NotDraft());
        if (_lines.Count == 0)
            return Result.Failure(Error.Validation("RepairEstimate.NoLines", "Add at least one item before submitting."));

        Status = RepairEstimateStatus.Submitted;
        SubmittedAt = at;
        SubmittedBy = actor;
        return Result.Success();
    }

    /*
        Records the decision of the approving party on a submitted version. Approval needs the party
        (e.g. the line operator) so the history says who agreed; a refusal or a change request needs the reason.
    */
    public Result Decide(
        RepairEstimateDecision decision, string? approverParty, string? approverName, string? approvalReference,
        string? note, DateTimeOffset at, string? actor)
    {
        if (Status != RepairEstimateStatus.Submitted)
            return Result.Failure(Error.Conflict("RepairEstimate.NotSubmitted",
                $"Version {Version} is {Status}; only a submitted version can be decided."));
        if (decision == RepairEstimateDecision.Approve && string.IsNullOrWhiteSpace(approverParty))
            return Result.Failure(Error.Validation("RepairEstimate.ApproverRequired", "Name the party that approved the estimate."));
        if (decision != RepairEstimateDecision.Approve && string.IsNullOrWhiteSpace(note))
            return Result.Failure(Error.Validation("RepairEstimate.ReasonRequired", "Give the reason of the refusal or the changes asked."));

        Status = decision switch
        {
            RepairEstimateDecision.Approve => RepairEstimateStatus.Approved,
            RepairEstimateDecision.Reject => RepairEstimateStatus.Rejected,
            _ => RepairEstimateStatus.ChangesRequested
        };
        DecidedAt = at;
        DecisionRecordedBy = actor;
        ApproverParty = Clean(approverParty);
        ApproverName = Clean(approverName);
        ApprovalReference = Clean(approvalReference);
        DecisionNote = Clean(note);
        return Result.Success();
    }

    // Payer of one item: allowed in every status, the amounts stay as decided
    public Result<RepairEstimateLine> SetPayer(Guid lineId, CostPayer payer, string? payerName, CostPayerStatus status, string? note)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return Result.Failure<RepairEstimateLine>(Error.NotFound("RepairEstimate.LineNotFound",
                $"Item '{lineId}' is not part of estimate version {Version}."));

        var set = line.SetPayer(payer, payerName, status, note);
        return set.IsFailure ? Result.Failure<RepairEstimateLine>(set.Error!) : Result.Success(line);
    }

    // "1 000 000 VND" style codes: three letters, upper case (VND, USD)
    private static string NormalizeCurrency(string currency) => currency.Trim().ToUpperInvariant();

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private Error NotDraft() => Error.Conflict("RepairEstimate.NotDraft",
        $"Version {Version} is {Status}; only a draft can be changed.");
}
