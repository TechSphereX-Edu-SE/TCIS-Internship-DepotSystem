using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// Thông tin cố định của một container (mục 2 tài liệu Depot).
/// Việc container đang ở trong bãi hay không được ghi ở <see cref="ContainerVisit"/>.
/// </summary>
public sealed class Container : AuditableEntity
{
    public string ContainerNumber { get; private set; } = default!;
    public string IsoCode { get; private set; } = default!;
    public ContainerSize Size { get; private set; }
    public ContainerCategory Category { get; private set; }
    public int MaxGrossWeightKg { get; private set; }
    public int TareWeightKg { get; private set; }
    public DateOnly? ManufactureDate { get; private set; }
    public string OwnerName { get; private set; } = default!;
    public ContainerCondition Condition { get; private set; } = ContainerCondition.Normal;
    public string? ConditionNote { get; private set; }

    private Container() { } // EF Core dùng khi đọc từ DB

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

    public void UpdateDetails(
        string isoCode,
        ContainerSize size,
        ContainerCategory category,
        int maxGrossWeightKg,
        int tareWeightKg,
        DateOnly? manufactureDate,
        string ownerName) =>
        SetDetails(isoCode, size, category, maxGrossWeightKg, tareWeightKg, manufactureDate, ownerName);

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
