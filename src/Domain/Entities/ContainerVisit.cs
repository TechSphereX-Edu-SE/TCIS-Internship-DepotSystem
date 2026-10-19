using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// Một vòng đời của container tại depot: từ lúc vào bãi (gate-in) đến lúc ra bãi (gate-out).
/// Một container có thể có nhiều vòng đời, nhưng tại một thời điểm chỉ có tối đa một vòng đời đang mở.
/// </summary>
public sealed class ContainerVisit : AuditableEntity
{
    public Guid ContainerId { get; private set; }
    public Container Container { get; private set; } = default!;
    public Guid LineOperatorId { get; private set; }
    public LineOperator LineOperator { get; private set; } = default!;

    // Vị trí hiện tại trong bãi (Bay/Row/Tier để trống nếu block ảo)
    public Guid BlockId { get; private set; }
    public Block Block { get; private set; } = default!;
    public int? Bay { get; private set; }
    public int? Row { get; private set; }
    public int? Tier { get; private set; }

    public ContainerGrade Grade { get; private set; }
    public ContainerCondition ConditionIn { get; private set; }
    public string? ConditionNoteIn { get; private set; }

    // Vào bãi
    public DateTimeOffset GateInAt { get; private set; }
    public string GateInVehicleNo { get; private set; } = default!;

    // Ra bãi
    public DateTimeOffset? GateOutAt { get; private set; }
    public string? GateOutVehicleNo { get; private set; }
    public Guid? DeliveryOrderId { get; private set; }
    public DeliveryOrder? DeliveryOrder { get; private set; }

    public bool IsInYard => GateOutAt is null;

    private ContainerVisit() { }

    /// <summary>Ghi nhận container vào bãi.</summary>
    public static Result<ContainerVisit> GateIn(
        Container container,
        Guid lineOperatorId,
        Block block,
        int? bay,
        int? row,
        int? tier,
        ContainerGrade grade,
        ContainerCondition condition,
        string? conditionNote,
        string vehicleNo,
        DateTimeOffset gateInAt)
    {
        var slot = block.ValidateSlot(bay, row, tier, container.Size);
        if (slot.IsFailure)
            return Result.Failure<ContainerVisit>(slot.Error!);

        container.UpdateCondition(condition, conditionNote);

        var visit = new ContainerVisit
        {
            ContainerId = container.Id,
            LineOperatorId = lineOperatorId,
            BlockId = block.Id,
            Bay = bay,
            Row = row,
            Tier = tier,
            Grade = grade,
            ConditionIn = condition,
            ConditionNoteIn = condition == ContainerCondition.Normal ? null : conditionNote?.Trim(),
            GateInAt = gateInAt,
            GateInVehicleNo = vehicleNo.Trim().ToUpperInvariant()
        };

        return Result.Success(visit);
    }

    /// <summary>Đảo chuyển container sang vị trí khác trong bãi.</summary>
    public Result MoveTo(Block block, int? bay, int? row, int? tier, ContainerSize size)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        var slot = block.ValidateSlot(bay, row, tier, size);
        if (slot.IsFailure)
            return slot;

        BlockId = block.Id;
        Bay = bay;
        Row = row;
        Tier = tier;
        return Result.Success();
    }

    /// <summary>Giao container ra khỏi bãi theo lệnh giao của hãng.</summary>
    public Result GateOut(DeliveryOrder order, ContainerSize size, ContainerCategory category, string vehicleNo, DateTimeOffset gateOutAt)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        if (order.LineOperatorId != LineOperatorId)
            return Result.Failure(Error.Conflict("ContainerVisit.WrongLineOperator",
                "The delivery order belongs to a different line operator."));

        var release = order.Release(size, category, DateOnly.FromDateTime(gateOutAt.Date));
        if (release.IsFailure)
            return release;

        DeliveryOrderId = order.Id;
        GateOutAt = gateOutAt;
        GateOutVehicleNo = vehicleNo.Trim().ToUpperInvariant();
        return Result.Success();
    }

    /// <summary>Số ngày tồn bãi tính đến thời điểm <paramref name="now"/>.</summary>
    public int DwellDays(DateTimeOffset now) => ((GateOutAt ?? now) - GateInAt).Days;
}
