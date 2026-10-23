using TechSpherex.CleanArchitecture.Domain.Common;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    One move of a container inside the depot (feedback 7: the visit history shows its moves).
    Written by the move command next to the visit change and never edited, so the visit keeps
    only the current position while this table keeps every step in between.
    From/To Bay/Row/Tier are null for a virtual block. Moves made before 23/10/2026 were not recorded.
*/
public sealed class ContainerMove : AuditableEntity
{
    public Guid VisitId { get; private set; }

    public Guid FromBlockId { get; private set; }
    public int? FromBay { get; private set; }
    public int? FromRow { get; private set; }
    public int? FromTier { get; private set; }

    public Guid ToBlockId { get; private set; }
    public int? ToBay { get; private set; }
    public int? ToRow { get; private set; }
    public int? ToTier { get; private set; }

    public DateTimeOffset MovedAt { get; private set; }
    public string? MovedBy { get; private set; }

    private ContainerMove() { }

    // The "from" position is the visit position before ContainerVisit.MoveTo, the "to" position the one after
    public static ContainerMove Record(
        Guid visitId,
        (Guid BlockId, int? Bay, int? Row, int? Tier) from,
        (Guid BlockId, int? Bay, int? Row, int? Tier) to,
        DateTimeOffset movedAt,
        string? movedBy) =>
        new()
        {
            VisitId = visitId,
            FromBlockId = from.BlockId,
            FromBay = from.Bay,
            FromRow = from.Row,
            FromTier = from.Tier,
            ToBlockId = to.BlockId,
            ToBay = to.Bay,
            ToRow = to.Row,
            ToTier = to.Tier,
            MovedAt = movedAt,
            MovedBy = movedBy
        };
}
