using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

// Every filter runs in SQL before paging, so the total matches the rows (feedback 8)
public sealed class SearchYardContainersQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<SearchYardContainersQuery, Result<PagedResult<YardContainerResponse>>>
{
    public async Task<Result<PagedResult<YardContainerResponse>>> HandleAsync(
        SearchYardContainersQuery query, CancellationToken cancellationToken = default)
    {
        var visits = dbContext.ContainerVisits.AsNoTracking().Where(v => v.GateOutAt == null);

        if (query.BlockId is { } blockId)
            visits = visits.Where(v => v.BlockId == blockId);
        if (query.Category is { } category)
            visits = visits.Where(v => v.Container.Category == category);
        if (query.Size is { } size)
            visits = visits.Where(v => v.Container.Size == size);
        // The operator of the visit (who the container came in for), not today's assignment
        if (query.OperatorId is { } operatorId)
            visits = visits.Where(v => v.LineOperatorId == operatorId);
        if (query.Repair is { } repair)
            visits = WithRepairState(visits, repair);

        visits = WithDwellDays(visits, query.MinDwellDays, query.MaxDwellDays, DepotTime.DateOf(timeProvider.GetUtcNow()));

        // Numbers are stored normalized (trim, upper case); "" = no search
        var search = string.IsNullOrWhiteSpace(query.Search) ? "" : ContainerNumberRules.Normalize(query.Search);
        if (search.Length > 0)
            visits = visits.Where(v => v.Container.ContainerNumber.Contains(search));

        var totalCount = await visits.CountAsync(cancellationToken);

        var ordered = search.Length > 0
            ? visits.OrderBy(v => v.Container.ContainerNumber == search ? 0 : v.Container.ContainerNumber.StartsWith(search) ? 1 : 2)
                .ThenBy(v => v.Container.ContainerNumber)
            : visits.OrderBy(v => v.Block.Code)
                .ThenBy(v => v.Row)
                .ThenBy(v => v.Bay)
                .ThenBy(v => v.Tier)
                .ThenBy(v => v.Container.ContainerNumber);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(YardProjections.ToYardContainer(dbContext))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<YardContainerResponse>(items, totalCount, query.Page, query.PageSize));
    }

    // Open case of the container, by its status (see YardRepairState)
    private IQueryable<ContainerVisit> WithRepairState(IQueryable<ContainerVisit> visits, YardRepairState state)
    {
        var open = dbContext.RepairCases.Where(c => c.Status != RepairCaseStatus.Passed);
        return state switch
        {
            YardRepairState.None => visits.Where(v => !open.Any(c => c.ContainerId == v.ContainerId)),
            YardRepairState.Abnormal => visits.Where(v =>
                open.Any(c => c.ContainerId == v.ContainerId && c.Status == RepairCaseStatus.AwaitingInspection)),
            YardRepairState.InRepair => visits.Where(v =>
                open.Any(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.AwaitingInspection)),
            _ => visits.Where(v => open.Any(c => c.ContainerId == v.ContainerId))
        };
    }

    /*
        Dwell days are Vietnam calendar days (DepotTime.DwellDays): 0 on the gate-in day.
        At least N days = came in on or before today - N; at most N days = on or after today - N.
        Turned into instants so the condition stays a plain comparison on GateInAt.
    */
    private static IQueryable<ContainerVisit> WithDwellDays(
        IQueryable<ContainerVisit> visits, int? minDays, int? maxDays, DateOnly today)
    {
        if (minDays is { } min)
        {
            var before = DepotTime.StartOf(today.AddDays(-min + 1));
            visits = visits.Where(v => v.GateInAt < before);
        }
        if (maxDays is { } max)
        {
            var from = DepotTime.StartOf(today.AddDays(-max));
            visits = visits.Where(v => v.GateInAt >= from);
        }
        return visits;
    }
}
