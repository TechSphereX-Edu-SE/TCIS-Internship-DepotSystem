using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;

/*
    Open visit = not gated out yet. Filters are optional, so the same query serves
    the whole yard, one block, or one row of a block (yard map).
    Sorted by block, row, bay, tier so a map can be drawn in reading order.
*/
public sealed class GetContainersInYardQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetContainersInYardQuery, Result<IReadOnlyList<YardContainerResponse>>>
{
    public async Task<Result<IReadOnlyList<YardContainerResponse>>> HandleAsync(
        GetContainersInYardQuery query, CancellationToken cancellationToken = default)
    {
        var containers = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.GateOutAt == null)
            .Where(v => query.BlockId == null || v.BlockId == query.BlockId)
            .Where(v => query.Row == null || v.Row == query.Row)
            .OrderBy(v => v.Block.Code)
            .ThenBy(v => v.Row)
            .ThenBy(v => v.Bay)
            .ThenBy(v => v.Tier)
            .Select(v => new YardContainerResponse(
                v.Id, v.ContainerId, v.Container.ContainerNumber, v.Container.IsoCode,
                v.Container.Size, v.Container.Category, v.Container.Condition,
                v.LineOperator.Code, v.BlockId, v.Block.Code,
                v.Bay, v.Row, v.Tier, v.GateInAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<YardContainerResponse>>(containers);
    }
}
