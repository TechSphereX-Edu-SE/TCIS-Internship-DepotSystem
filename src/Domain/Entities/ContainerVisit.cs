using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// One lifecycle of a container at the depot: from gate-in to gate-out.
/// A container can have many visits, but at most one open visit at any time.
/// </summary>
public sealed class ContainerVisit : AuditableEntity
{
    public Guid ContainerId { get; private set; }
    public Container Container { get; private set; } = default!;
    public Guid LineOperatorId { get; private set; }
    public LineOperator LineOperator { get; private set; } = default!;

    // Current yard position (Bay/Row/Tier are null for a virtual block)
    public Guid BlockId { get; private set; }
    public Block Block { get; private set; } = default!;
    public int? Bay { get; private set; }
    public int? Row { get; private set; }
    public int? Tier { get; private set; }

    public ContainerGrade Grade { get; private set; }
    public ContainerCondition ConditionIn { get; private set; }
    public string? ConditionNoteIn { get; private set; }

    // Gate-in
    public DateTimeOffset GateInAt { get; private set; }
    public string GateInVehicleNo { get; private set; } = default!;

    // Gate-out
    public DateTimeOffset? GateOutAt { get; private set; }
    public string? GateOutVehicleNo { get; private set; }
    public Guid? DeliveryOrderId { get; private set; }
    public DeliveryOrder? DeliveryOrder { get; private set; }

    public bool IsInYard => GateOutAt is null;

    private ContainerVisit() { }

    /// <summary>Records a container entering the yard.</summary>
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

    /// <summary>Moves the container to another position in the yard.</summary>
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

    /// <summary>Releases the container under a delivery order of the same line operator.</summary>
    public Result GateOut(DeliveryOrder order, ContainerCategory category, string vehicleNo, DateTimeOffset gateOutAt)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        if (order.LineOperatorId != LineOperatorId)
            return Result.Failure(Error.Conflict("ContainerVisit.WrongLineOperator",
                "The delivery order belongs to a different line operator."));

        var release = order.Release(category, DateOnly.FromDateTime(gateOutAt.Date));
        if (release.IsFailure)
            return release;

        DeliveryOrderId = order.Id;
        GateOutAt = gateOutAt;
        GateOutVehicleNo = vehicleNo.Trim().ToUpperInvariant();
        return Result.Success();
    }

    /// <summary>Number of days the container has stayed in the yard as of <paramref name="now"/>.</summary>
    public int DwellDays(DateTimeOffset now) => ((GateOutAt ?? now) - GateInAt).Days;
}
