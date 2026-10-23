using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/* Container master data (section 2 of the depot spec).
Whether the container is currently in the yard is recorded in ContainerVisit. */
public sealed class Container : AuditableEntity
{
    public string ContainerNumber { get; private set; } = default!;
    public string IsoCode { get; private set; } = default!;
    public ContainerSize Size { get; private set; }
    public ContainerCategory Category { get; private set; }
    public int MaxGrossWeightKg { get; private set; }
    public int TareWeightKg { get; private set; }
    public DateOnly? ManufactureDate { get; private set; }

    /*
        Owner as free text, as it was first recorded. Before 23/10/2026 this was the only owner field,
        so it is kept unchanged for reconciliation (feedback 9: không tự đoán hoặc làm mất giá trị).
        The owner itself is OwnerId (a partner with the Owner role).
    */
    public string OwnerName { get; private set; } = default!;

    // Owner partner (feedback 2: stored by id, not by name or number prefix); null until reconciled
    public Guid? OwnerId { get; private set; }
    public LineOperator? Owner { get; private set; }

    public ContainerCondition Condition { get; private set; } = ContainerCondition.Normal;
    public string? ConditionNote { get; private set; }

    private Container() { } // Used by EF Core when loading from the database

    public Container(
        string containerNumber,
        string isoCode,
        ContainerSize size,
        ContainerCategory category,
        int maxGrossWeightKg,
        int tareWeightKg,
        DateOnly? manufactureDate,
        string ownerName)
    {
        ContainerNumber = ContainerNumberRules.Normalize(containerNumber);
        SetDetails(isoCode, size, category, maxGrossWeightKg, tareWeightKg, manufactureDate, ownerName);
    }

    // The owner text recorded at creation is not changed here: the owner is set with SetOwner
    public void UpdateDetails(
        string isoCode,
        ContainerSize size,
        ContainerCategory category,
        int maxGrossWeightKg,
        int tareWeightKg,
        DateOnly? manufactureDate) =>
        SetDetails(isoCode, size, category, maxGrossWeightKg, tareWeightKg, manufactureDate, OwnerName);

    // Only a partner with the Owner role can own a container (409 otherwise)
    public Result SetOwner(LineOperator owner)
    {
        if (!owner.IsOwner)
            return Result.Failure(Error.Conflict("Partner.NotOwner",
                $"Partner {owner.Code} is not marked as a container owner."));

        OwnerId = owner.Id;
        Owner = owner;
        return Result.Success();
    }

    public void UpdateCondition(ContainerCondition condition, string? note)
    {
        Condition = condition;
        ConditionNote = condition == ContainerCondition.Normal ? null : note?.Trim();
    }

    private void SetDetails(
        string isoCode,
        ContainerSize size,
        ContainerCategory category,
        int maxGrossWeightKg,
        int tareWeightKg,
        DateOnly? manufactureDate,
        string ownerName)
    {
        IsoCode = isoCode.Trim().ToUpperInvariant();
        Size = size;
        Category = category;
        MaxGrossWeightKg = maxGrossWeightKg;
        TareWeightKg = tareWeightKg;
        ManufactureDate = manufactureDate;
        OwnerName = ownerName.Trim();
    }
}
