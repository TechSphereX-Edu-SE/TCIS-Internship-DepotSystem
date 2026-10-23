using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.ThroughputEvents;

// Filters run in SQL before paging (ThroughputFiltering, shared with the totals)
public sealed class GetThroughputEventsQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetThroughputEventsQuery, Result<PagedResult<VisitHistoryRow>>>
{
    public async Task<Result<PagedResult<VisitHistoryRow>>> HandleAsync(
        GetThroughputEventsQuery query, CancellationToken cancellationToken = default)
    {
        var visits = query.Kind == ThroughputEventKind.GateIn
            ? dbContext.GateInsOf(query).OrderByDescending(v => v.GateInAt)
            : dbContext.GateOutsOf(query).OrderByDescending(v => v.GateOutAt);

        var totalCount = await visits.CountAsync(cancellationToken);

        var items = await visits
            .ThenBy(v => v.Container.ContainerNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToHistoryRowsAsync(dbContext, timeProvider.GetUtcNow(), cancellationToken);

        return Result.Success(new PagedResult<VisitHistoryRow>(items, totalCount, query.Page, query.PageSize));
    }
}
