using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/* 
    One lifecycle of a container at the depot: from gate-in to gate-out
    A container can have many visits, but at most one open visit at any time
*/
public sealed class ContainerVisit : AuditableEntity
{
    public Guid ContainerId { get; private set; }
    public Container Container { get; private set; } = default!;
    public Guid LineOperatorId { get; private set; }
    public LineOperator LineOperator { get; private set; } = default!;

    /*
        Operating assignment the line operator was taken from at gate-in (feedback 2: căn cứ phân công).
        Null for visits recorded before assignments existed. Never changed afterwards (Q5).
    */
    public Guid? OperatingAssignmentId { get; private set; }

    /*
        Owner of the container at gate-in (feedback 7: chủ sở hữu lịch sử). Copied once from the container,
        so a later owner change does not rewrite past visits. Null for visits recorded before 23/10/2026
        and when the container had no reconciled owner yet: the history then says "không có dữ liệu"
        instead of showing today's owner.
    */
    public Guid? OwnerId { get; private set; }
    public LineOperator? Owner { get; private set; }

    // Current yard position (Bay/Row/Tier are null for a virtual block)
    public Guid BlockId { get; private set; }
    public Block Block { get; private set; } = default!;
    public int? Bay { get; private set; }
    public int? Row { get; private set; }
    public int? Tier { get; private set; }

    /*
        Position at gate-in (feedback 3: the gate-in table shows the block the container entered).
        Kept apart from the current position, which changes with every move.
        Null for visits recorded before this was stored: their gate-in position is unknown and is not
        rebuilt from the current position.
    */
    public Guid? GateInBlockId { get; private set; }
    public Block? GateInBlock { get; private set; }
    public int? GateInBay { get; private set; }
    public int? GateInRow { get; private set; }
    public int? GateInTier { get; private set; }

    // Legacy A/B/C grade of visits recorded before 23/10/2026; null for every newer visit
    public ContainerGrade? Grade { get; private set; }
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

    // Feedback 6: cấp rỗng (with a delivery order) or chuyển sửa ngoài (without); null while in the yard
    public GateOutPurpose? ExitPurpose { get; private set; }

    // PostgreSQL xmin: two gate-outs (or a gate-out and a move) of the same container cannot both succeed
    public uint RowVersion { get; private set; }

    public bool IsInYard => GateOutAt is null;

    private ContainerVisit() { }

    // Records a container entering the yard
    public static Result<ContainerVisit> GateIn(
        Container container,
        Guid lineOperatorId,
        Block block,
        int? bay,
        int? row,
        int? tier,
        ContainerCondition condition,
        string? conditionNote,
        string vehicleNo,
        DateTimeOffset gateInAt,
        OperatingAssignment? basis = null)
    {
        // The visit's operator must be the one of the assignment it is based on
        if (basis is not null && (basis.OperatorId != lineOperatorId || basis.ContainerId != container.Id))
            return Result.Failure<ContainerVisit>(Error.Conflict("ContainerVisit.OperatorMismatch",
                "The line operator does not match the confirmed operating assignment of the container."));

        // The block must take this container type before its position is even looked at
        var accepts = block.CheckAccepts(container.Category);
        if (accepts.IsFailure)
            return Result.Failure<ContainerVisit>(accepts.Error!);

        var slot = block.ValidateSlot(bay, row, tier, container.Size);
        if (slot.IsFailure)
            return Result.Failure<ContainerVisit>(slot.Error!);

        container.UpdateCondition(condition, conditionNote);
        block.RecordContainerChange();

        var visit = new ContainerVisit
        {
            ContainerId = container.Id,
            LineOperatorId = lineOperatorId,
            OperatingAssignmentId = basis?.Id,
            OwnerId = container.OwnerId,
            BlockId = block.Id,
            Bay = bay,
            Row = row,
            Tier = tier,
            GateInBlockId = block.Id,
            GateInBay = bay,
            GateInRow = row,
            GateInTier = tier,
            ConditionIn = condition,
            ConditionNoteIn = condition == ContainerCondition.Normal ? null : conditionNote?.Trim(),
            GateInAt = gateInAt,
            GateInVehicleNo = vehicleNo.Trim().ToUpperInvariant()
        };

        return Result.Success(visit);
    }

    // Moves the container to another position in the yard
    public Result MoveTo(Block block, int? bay, int? row, int? tier, ContainerSize size)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        var slot = block.ValidateSlot(bay, row, tier, size);
        if (slot.IsFailure)
            return slot;

        // Both blocks change: the old one loses the container, the new one gets it (see Block.PlacementVersion).
        // The old block is the loaded navigation (null when the caller did not load it, e.g. in unit tests).
        Block?.RecordContainerChange();
        block.RecordContainerChange();
        // Navigation and key together, so a loaded Block never points to the old block
        Block = block;
        BlockId = block.Id;
        Bay = bay;
        Row = row;
        Tier = tier;
        return Result.Success();
    }

    // Releases the container (cấp rỗng) under a delivery order of the same line operator, using the line of its type and size
    public Result GateOut(
        DeliveryOrder order, ContainerCategory category, ContainerSize size, string vehicleNo, DateTimeOffset gateOutAt)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        if (order.LineOperatorId != LineOperatorId)
            return Result.Failure(Error.Conflict("ContainerVisit.WrongLineOperator",
                "The delivery order belongs to a different line operator."));

        // The expiry date is a Vietnam date: the order compares it with the Vietnam date of the gate-out
        // (gateOutAt.Date would depend on the offset the instant carries, e.g. UTC)
        var release = order.Release(category, size, gateOutAt);
        if (release.IsFailure)
            return release;

        DeliveryOrderId = order.Id;
        Leave(GateOutPurpose.EmptyRelease, vehicleNo, gateOutAt);
        return Result.Success();
    }

    /*
        Feedback 4: the container leaves for an outside repair facility. No delivery order is used and
        its condition stays Damaged; the transfer (ExternalRepairTransfer) records where it went.
    */
    public Result GateOutForRepair(string vehicleNo, DateTimeOffset gateOutAt)
    {
        if (!IsInYard)
            return Result.Failure(Error.Conflict("ContainerVisit.AlreadyGatedOut", "Container has already left the depot."));

        Leave(GateOutPurpose.ExternalRepair, vehicleNo, gateOutAt);
        return Result.Success();
    }

    private void Leave(GateOutPurpose purpose, string vehicleNo, DateTimeOffset gateOutAt)
    {
        // The container leaves its block (see Block.PlacementVersion); null when the block was not loaded
        Block?.RecordContainerChange();
        ExitPurpose = purpose;
        GateOutAt = gateOutAt;
        GateOutVehicleNo = vehicleNo.Trim().ToUpperInvariant();
    }

    // Vietnam calendar days in the yard as of <paramref name="now"/> (0 on the gate-in day)
    public int DwellDays(DateTimeOffset now) => DepotTime.DwellDays(GateInAt, GateOutAt ?? now);
}
