using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

// Every filter runs in SQL before paging, so the total matches the rows (feedback 8)
public sealed class SearchVisitHistoryQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<SearchVisitHistoryQuery, Result<PagedResult<VisitHistoryRow>>>
{
    public async Task<Result<PagedResult<VisitHistoryRow>>> HandleAsync(
        SearchVisitHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var visits = dbContext.ContainerVisits.AsNoTracking();

        if (query.ContainerId is { } containerId)
            visits = visits.Where(v => v.ContainerId == containerId);

        // Numbers are stored normalized (trim, upper case)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = ContainerNumberRules.Normalize(query.Search);
            visits = visits.Where(v => v.Container.ContainerNumber.Contains(search));
        }

        // The operator of that visit, never the container's operator today (feedback 8)
        if (query.OperatorId is { } operatorId)
            visits = visits.Where(v => v.LineOperatorId == operatorId);
        if (query.Category is { } category)
            visits = visits.Where(v => v.Container.Category == category);
        if (query.Size is { } size)
            visits = visits.Where(v => v.Container.Size == size);
        if (query.State is { } state)
            visits = state == VisitState.Open
                ? visits.Where(v => v.GateOutAt == null)
                : visits.Where(v => v.GateOutAt != null);
        if (query.Purpose is { } purpose)
            visits = visits.Where(v => v.ExitPurpose == purpose);

        visits = InPeriod(visits, query);

        var totalCount = await visits.CountAsync(cancellationToken);

        // Newest first by the event looked at; open visits have no gate-out and go after the others
        var ordered = query.Event == VisitHistoryEvent.GateOut
            ? visits.OrderBy(v => v.GateOutAt == null).ThenByDescending(v => v.GateOutAt).ThenByDescending(v => v.GateInAt)
            : visits.OrderByDescending(v => v.GateInAt);

        var items = await ordered
            .ThenBy(v => v.Container.ContainerNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToHistoryRowsAsync(dbContext, timeProvider.GetUtcNow(), cancellationToken);

        return Result.Success(new PagedResult<VisitHistoryRow>(items, totalCount, query.Page, query.PageSize));
    }

    /*
        Period [From 00:00 +07, To+1 00:00 +07), open on a side without a date.
        GateIn looks at the gate-in time, GateOut at the gate-out time (a visit with no gate-out never matches),
        Any at either: a visit that came in before the period and left in it is found once.
        Only the given bounds become conditions, so no artificial min/max dates are sent to the database.
    */
    private static IQueryable<ContainerVisit> InPeriod(IQueryable<ContainerVisit> visits, SearchVisitHistoryQuery query)
    {
        DateTimeOffset? start = query.From is { } from ? DepotTime.StartOf(from) : null;
        DateTimeOffset? end = query.To is { } to ? DepotTime.StartOf(to.AddDays(1)) : null;
        var hasStart = start is not null;
        var hasEnd = end is not null;

        // "!hasStart ||" drops the bound when it is not given (EF turns the constant into SQL true/false)
        return query.Event switch
        {
            VisitHistoryEvent.GateIn => visits.Where(v =>
                (!hasStart || v.GateInAt >= start) && (!hasEnd || v.GateInAt < end)),
            VisitHistoryEvent.GateOut => visits.Where(v =>
                v.GateOutAt != null && (!hasStart || v.GateOutAt >= start) && (!hasEnd || v.GateOutAt < end)),
            _ when !hasStart && !hasEnd => visits,
            _ => visits.Where(v =>
                ((!hasStart || v.GateInAt >= start) && (!hasEnd || v.GateInAt < end)) ||
                (v.GateOutAt != null && (!hasStart || v.GateOutAt >= start) && (!hasEnd || v.GateOutAt < end)))
        };
    }
}
