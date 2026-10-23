using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    One damage item of an estimate (feedback 4: từng hạng mục hư, cách sửa, dự toán, bên chịu phí).
    FindingId links the item to the reported damage it costs, when there is one.
    The payer belongs to the item, so one case can split its cost between several parties; it can still
    change after approval (allocation, dispute and its outcome), the amounts cannot.
*/
public sealed class RepairEstimateLine : BaseEntity
{
    public const int MaxTextLength = 500;

    public Guid EstimateId { get; private set; }
    public Guid? FindingId { get; private set; }
    public int Position { get; private set; }
    public string DamageDescription { get; private set; } = default!;
    public string RepairMethod { get; private set; } = default!;
    public decimal Amount { get; private set; }

    public CostPayer Payer { get; private set; }
    // Name of the party when the role alone does not say who (e.g. Other, or which customer)
    public string? PayerName { get; private set; }
    public CostPayerStatus PayerStatus { get; private set; }
    // Dispute reason or allocation result
    public string? PayerNote { get; private set; }

    private RepairEstimateLine() { }

    internal static Result<RepairEstimateLine> Create(
        Guid estimateId, int position, Guid? findingId, string damageDescription, string repairMethod, decimal amount,
        CostPayer payer, string? payerName)
    {
        if (string.IsNullOrWhiteSpace(damageDescription) || string.IsNullOrWhiteSpace(repairMethod))
            return Result.Failure<RepairEstimateLine>(Error.Validation("RepairEstimate.LineIncomplete",
                "Each item needs the damage and the repair method."));
        if (amount < 0)
            return Result.Failure<RepairEstimateLine>(Error.Validation("RepairEstimate.NegativeAmount",
                "An item cannot cost less than 0."));

        var line = new RepairEstimateLine
        {
            EstimateId = estimateId,
            Position = position,
            FindingId = findingId,
            DamageDescription = damageDescription.Trim(),
            RepairMethod = repairMethod.Trim(),
            Amount = amount
        };
        var payerSet = line.SetPayer(payer, payerName, CostPayerStatus.Expected, null);
        return payerSet.IsFailure ? Result.Failure<RepairEstimateLine>(payerSet.Error!) : Result.Success(line);
    }

    // Copy for a new version of the estimate (same items, payers kept)
    internal RepairEstimateLine CopyTo(Guid estimateId) => new()
    {
        EstimateId = estimateId,
        Position = Position,
        FindingId = FindingId,
        DamageDescription = DamageDescription,
        RepairMethod = RepairMethod,
        Amount = Amount,
        Payer = Payer,
        PayerName = PayerName,
        PayerStatus = PayerStatus,
        PayerNote = PayerNote
    };

    /*
        "Other" needs the party's name; a dispute needs its reason. Agreed with Undetermined makes no sense:
        an allocation is only settled once someone is named.
    */
    internal Result SetPayer(CostPayer payer, string? payerName, CostPayerStatus status, string? note)
    {
        var name = string.IsNullOrWhiteSpace(payerName) ? null : payerName.Trim();
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (payer == CostPayer.Other && name is null)
            return Result.Failure(Error.Validation("RepairEstimate.PayerNameRequired",
                "Name the party that pays when it is 'Other'."));
        if (status == CostPayerStatus.Disputed && cleanNote is null)
            return Result.Failure(Error.Validation("RepairEstimate.DisputeReasonRequired",
                "Give the reason of the dispute."));
        if (status == CostPayerStatus.Agreed && payer == CostPayer.Undetermined)
            return Result.Failure(Error.Validation("RepairEstimate.PayerUndetermined",
                "Choose who pays before marking the allocation as agreed."));

        Payer = payer;
        PayerName = name;
        PayerStatus = status;
        PayerNote = cleanNote;
        return Result.Success();
    }
}
