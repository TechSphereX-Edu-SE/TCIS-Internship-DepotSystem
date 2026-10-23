using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

// Reads the gate times of every visit of the container and sums them in memory (Vietnam calendar days)
public sealed class GetContainerVisitSummaryQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetContainerVisitSummaryQuery, Result<ContainerVisitSummary>>
{
    public async Task<Result<ContainerVisitSummary>> HandleAsync(
        GetContainerVisitSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers
            .AsNoTracking()
            .Where(c => c.Id == query.ContainerId)
            .Select(c => new
            {
                c.Id, c.ContainerNumber, c.IsoCode, c.Category, c.Size,
                OwnerCode = c.Owner != null ? c.Owner.Code : null,
                OwnerName = c.Owner != null ? c.Owner.Name : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (container is null)
            return Result.Failure<ContainerVisitSummary>(Error.NotFound("Container.NotFound",
                $"Container with ID '{query.ContainerId}' was not found."));

        var now = timeProvider.GetUtcNow();
        var visits = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.ContainerId == container.Id)
            .Select(v => new { v.Id, v.GateInAt, v.GateOutAt })
            .ToListAsync(cancellationToken);

        var open = visits.FirstOrDefault(v => v.GateOutAt is null);
        var presence = await PresenceAsync(container.Id, visits.Count, open is not null, cancellationToken);

        return Result.Success(new ContainerVisitSummary(
            container.Id, container.ContainerNumber, container.IsoCode, container.Category, container.Size,
            container.OwnerCode, container.OwnerName,
            visits.Count,
            visits.Count(v => v.GateOutAt is not null),
            presence,
            open?.Id,
            open is null ? null : DepotTime.DwellDays(open.GateInAt, now),
            visits.Sum(v => DepotTime.DwellDays(v.GateInAt, v.GateOutAt ?? now)),
            now));
    }

    // Out of the depot is split: at an outside repair facility (transfer gated out, not back yet) or simply out
    private async Task<ContainerPresence> PresenceAsync(
        Guid containerId, int visitCount, bool inDepot, CancellationToken cancellationToken)
    {
        if (inDepot)
            return ContainerPresence.InDepot;
        if (visitCount == 0)
            return ContainerPresence.NeverIn;

        var atRepair = await dbContext.ExternalRepairTransfers.AnyAsync(
            t => t.ContainerId == containerId && t.Status == ExternalRepairStatus.GatedOut, cancellationToken);
        return atRepair ? ContainerPresence.AtOutsideRepair : ContainerPresence.OutOfDepot;
    }
}
