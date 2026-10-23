using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Shared;

/*
    Feedback 23/10 (gate-in dropdown): where the container is right now, so the clerk sees before picking it
    whether it can come in, and which screen resolves what blocks it.
    - InYard + BlockCode/Bay/Row/Tier/GateInAt: the open visit (Bay/Row/Tier null for a virtual block).
    - LastGateOutAt/LastExitPurpose: the last closed visit (cấp rỗng / chuyển sửa ngoài), null if it never came in.
    - OpenRepairCaseId/OpenRepairStatus: the repair case that is not Passed yet (it blocks release).
    - OutsideRepairFacility/OutsideRepairStatus: an active outside repair transfer
      (Requested = still in the yard waiting to leave, GatedOut = at the facility, the next gate-in is its return).
*/
public sealed record ContainerSituation(
    bool InYard,
    string? BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    DateTimeOffset? GateInAt,
    DateTimeOffset? LastGateOutAt,
    string? LastExitPurpose,
    Guid? OpenRepairCaseId,
    string? OpenRepairStatus,
    string? OutsideRepairFacility,
    string? OutsideRepairStatus);

public static class ContainerSituationQueries
{
    /*
        Situation of every container of a page, in three queries (visits, open cases, active transfers)
        instead of three per container. Only small projections are read, no entity is tracked.
    */
    public static async Task<IReadOnlyDictionary<Guid, ContainerSituation>> LoadSituationsAsync(
        this IAppDbContext dbContext, IReadOnlyCollection<Guid> containerIds, CancellationToken cancellationToken)
    {
        if (containerIds.Count == 0)
            return new Dictionary<Guid, ContainerSituation>();

        var ids = containerIds.ToList();

        // All visits of these containers: the open one gives the position, the latest closed one the last exit
        var visits = await dbContext.ContainerVisits.AsNoTracking()
            .Where(v => ids.Contains(v.ContainerId))
            .Select(v => new { v.ContainerId, BlockCode = v.Block.Code, v.Bay, v.Row, v.Tier, v.GateInAt, v.GateOutAt, v.ExitPurpose })
            .ToListAsync(cancellationToken);

        var openCases = await dbContext.RepairCases.AsNoTracking()
            .Where(c => ids.Contains(c.ContainerId) && c.Status != RepairCaseStatus.Passed)
            .Select(c => new { c.Id, c.ContainerId, c.Status, c.OpenedAt })
            .ToListAsync(cancellationToken);

        var activeTransfers = await dbContext.ExternalRepairTransfers.AsNoTracking()
            .Where(t => ids.Contains(t.ContainerId) &&
                        (t.Status == ExternalRepairStatus.Requested || t.Status == ExternalRepairStatus.GatedOut))
            .Select(t => new { t.ContainerId, t.Facility, t.Status, t.RequestedAt })
            .ToListAsync(cancellationToken);

        // One open visit / open case / active transfer per container is the rule; the newest wins if data disagrees
        return ids.Distinct().ToDictionary(id => id, id =>
        {
            var open = visits.Where(v => v.ContainerId == id && v.GateOutAt == null).MaxBy(v => v.GateInAt);
            var lastExit = visits.Where(v => v.ContainerId == id && v.GateOutAt != null).MaxBy(v => v.GateOutAt);
            var repairCase = openCases.Where(c => c.ContainerId == id).MaxBy(c => c.OpenedAt);
            var transfer = activeTransfers.Where(t => t.ContainerId == id).MaxBy(t => t.RequestedAt);

            return new ContainerSituation(
                open is not null,
                open?.BlockCode, open?.Bay, open?.Row, open?.Tier, open?.GateInAt,
                lastExit?.GateOutAt, lastExit?.ExitPurpose?.ToString(),
                repairCase?.Id, repairCase?.Status.ToString(),
                transfer?.Facility, transfer?.Status.ToString());
        });
    }
}
