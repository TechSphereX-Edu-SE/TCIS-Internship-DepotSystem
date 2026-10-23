using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Inspection & repair case of a container (feedback 4: "hồ sơ công việc", not a yard area).
    - One open case per container: a new damage report is added to the open case as a finding,
      so the same damage is not opened twice (unique index on ContainerId while not Passed).
    - While open, the container cannot be released (cấp rỗng); Passed lifts only this block.
    - The case never moves the container: inspection and repair happen where it is, or staff use
      the move screen. The condition at gate-in stays on the visit; findings, photos and history stay.
    Children (findings, history rows, photos) are separate entities created here and added by the
    handler, so the aggregate does not load its whole history to change its status.
*/
public sealed class RepairCase : AuditableEntity
{
    public Guid ContainerId { get; private set; }
    public Container Container { get; private set; } = default!;

    // Visit during which the case was opened; null when the container was not in the yard
    public Guid? VisitId { get; private set; }
    public ContainerVisit? Visit { get; private set; }

    public RepairCaseStatus Status { get; private set; }

    // Inspection conclusion: false = passed without repair, true = repair needed, null = not inspected yet
    public bool? RepairNeeded { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    // PostgreSQL xmin: two people acting on the same case at once cannot both win
    public uint RowVersion { get; private set; }

    public bool IsOpen => Status != RepairCaseStatus.Passed;

    private RepairCase() { }

    // Allowed status before each action, and the status after it
    public static readonly IReadOnlyDictionary<RepairAction, (RepairCaseStatus From, RepairCaseStatus To)> Transitions =
        new Dictionary<RepairAction, (RepairCaseStatus, RepairCaseStatus)>
        {
            [RepairAction.InspectPassed] = (RepairCaseStatus.AwaitingInspection, RepairCaseStatus.Passed),
            [RepairAction.InspectNeedsRepair] = (RepairCaseStatus.AwaitingInspection, RepairCaseStatus.AwaitingEstimateApproval),
            [RepairAction.ApproveRepair] = (RepairCaseStatus.AwaitingEstimateApproval, RepairCaseStatus.RepairApproved),
            [RepairAction.StartRepair] = (RepairCaseStatus.RepairApproved, RepairCaseStatus.InRepair),
            [RepairAction.CompleteRepair] = (RepairCaseStatus.InRepair, RepairCaseStatus.AwaitingAcceptance),
            [RepairAction.AcceptPassed] = (RepairCaseStatus.AwaitingAcceptance, RepairCaseStatus.Passed),
            [RepairAction.AcceptRejected] = (RepairCaseStatus.AwaitingAcceptance, RepairCaseStatus.InRepair)
        };

    /*
        Actions the screen can offer for a status (same table as Apply uses).
        ApproveRepair is left out: it happens by approving an estimate version (ApproveEstimate, feedback 4).
    */
    public static IReadOnlyList<RepairAction> ActionsFrom(RepairCaseStatus status) =>
        Transitions.Where(t => t.Value.From == status && t.Key != RepairAction.ApproveRepair).Select(t => t.Key).ToList();

    /*
        Opens a case with its first finding. The container is marked Damaged with the description,
        which (with the open case) keeps it from being released.
    */
    public static (RepairCase Case, RepairFinding Finding, RepairCaseEvent Opened) Open(
        Container container, Guid? visitId, string description, RepairFindingSource source,
        DateTimeOffset at, string? actor)
    {
        var repairCase = new RepairCase
        {
            ContainerId = container.Id,
            VisitId = visitId,
            Status = RepairCaseStatus.AwaitingInspection,
            OpenedAt = at
        };
        var finding = RepairFinding.Create(repairCase, description, source, visitId, at, actor);
        var opened = RepairCaseEvent.Create(repairCase, RepairEventKind.Opened, null, repairCase.Status, finding.Description, at, actor);

        container.UpdateCondition(ContainerCondition.Damaged, finding.Description);
        return (repairCase, finding, opened);
    }

    // Feedback 4: a new report on a container with an open case goes into that case
    public Result<(RepairFinding Finding, RepairCaseEvent Added)> AddFinding(
        string description, RepairFindingSource source, Guid? visitId, DateTimeOffset at, string? actor)
    {
        if (!IsOpen)
            return Result.Failure<(RepairFinding, RepairCaseEvent)>(Closed());

        var finding = RepairFinding.Create(this, description, source, visitId, at, actor);
        var added = RepairCaseEvent.Create(this, RepairEventKind.FindingAdded, Status, Status, finding.Description, at, actor);
        return Result.Success((finding, added));
    }

    /*
        Moves the case one step. Passed (inspection without repair, or accepted after repair) closes it
        and sets the container back to Normal: it only lifts the repair block, nothing is released.
        "Sửa lại" (AcceptRejected) needs a reason, so the repairer knows what to redo.
    */
    public Result<RepairCaseEvent> Apply(RepairAction action, string? note, Container container, DateTimeOffset at, string? actor)
    {
        if (container.Id != ContainerId)
            throw new InvalidOperationException("The container does not belong to this repair case.");

        if (!Transitions.TryGetValue(action, out var step))
            return Result.Failure<RepairCaseEvent>(Error.Validation("RepairCase.UnknownAction", $"Unknown action '{action}'."));

        // Feedback 4: the repair is approved together with its cost, never without an approved estimate
        if (action == RepairAction.ApproveRepair)
            return Result.Failure<RepairCaseEvent>(Error.Conflict("RepairCase.EstimateRequired",
                "The repair is approved by approving a submitted estimate version."));

        return Move(action, step, note, container, at, actor);
    }

    /*
        Feedback 4: approving the estimate is the approval of the repair. The first approved version moves
        the case from AwaitingEstimateApproval to RepairApproved; a later version (extra damage found while
        repairing) is recorded without changing the status.
    */
    public Result<RepairCaseEvent> ApproveEstimate(RepairEstimate estimate, DateTimeOffset at, string? actor)
    {
        if (estimate.RepairCaseId != Id)
            throw new InvalidOperationException("The estimate does not belong to this repair case.");
        if (!IsOpen)
            return Result.Failure<RepairCaseEvent>(Closed());
        if (estimate.Status != RepairEstimateStatus.Approved)
            return Result.Failure<RepairCaseEvent>(Error.Conflict("RepairEstimate.NotApproved",
                $"Version {estimate.Version} is {estimate.Status}, not approved."));

        var note = $"v{estimate.Version} · {estimate.Total:0.##} {estimate.Currency} · {estimate.ApproverParty}";
        if (Status != RepairCaseStatus.AwaitingEstimateApproval)
            return Result.Success(Record(RepairEventKind.EstimateApproved, note, at, actor));

        var from = Status;
        Status = Transitions[RepairAction.ApproveRepair].To;
        return Result.Success(RepairCaseEvent.Create(this, RepairEventKind.EstimateApproved, from, Status, note, at, actor));
    }

    private Result<RepairCaseEvent> Move(
        RepairAction action, (RepairCaseStatus From, RepairCaseStatus To) step, string? note, Container container,
        DateTimeOffset at, string? actor)
    {
        if (Status != step.From)
            return Result.Failure<RepairCaseEvent>(Error.Conflict("RepairCase.InvalidTransition",
                $"'{action}' is not allowed while the case is {Status}."));

        if (action == RepairAction.AcceptRejected && string.IsNullOrWhiteSpace(note))
            return Result.Failure<RepairCaseEvent>(Error.Validation("RepairCase.ReasonRequired",
                "Give the reason the repair is not accepted."));

        var from = Status;
        Status = step.To;
        if (action is RepairAction.InspectPassed or RepairAction.InspectNeedsRepair)
            RepairNeeded = action == RepairAction.InspectNeedsRepair;

        if (Status == RepairCaseStatus.Passed)
        {
            ClosedAt = at;
            container.UpdateCondition(ContainerCondition.Normal, null);
        }

        var kind = Enum.Parse<RepairEventKind>(action.ToString());
        return Result.Success(RepairCaseEvent.Create(this, kind, from, Status, note, at, actor));
    }

    // History row that does not change the status (external repair steps)
    public RepairCaseEvent Record(RepairEventKind kind, string? note, DateTimeOffset at, string? actor) =>
        RepairCaseEvent.Create(this, kind, Status, Status, note, at, actor);

    public static Error Closed() =>
        Error.Conflict("RepairCase.Closed", "The repair case is already closed.");
}
