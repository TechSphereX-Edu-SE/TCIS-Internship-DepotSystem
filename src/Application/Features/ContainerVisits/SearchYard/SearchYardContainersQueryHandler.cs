using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

// Every filter runs in SQL before paging (shared YardVisitFiltering), so the total matches the rows (feedback 8)
public sealed class SearchYardContainersQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<SearchYardContainersQuery, Result<PagedResult<YardContainerResponse>>>
{
    public async Task<Result<PagedResult<YardContainerResponse>>> HandleAsync(
        SearchYardContainersQuery query, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        var visits = dbContext.InYard(query, today).AsNoTracking();

        // Numbers are stored normalized (trim, upper case); "" = no search
        var search = string.IsNullOrWhiteSpace(query.Search) ? "" : ContainerNumberRules.Normalize(query.Search);
        if (search.Length > 0)
            visits = visits.Where(v => v.Container.ContainerNumber.Contains(search));

        var totalCount = await visits.CountAsync(cancellationToken);

        var ordered = search.Length > 0
            ? visits.OrderBy(v => v.Container.ContainerNumber == search ? 0 : v.Container.ContainerNumber.StartsWith(search) ? 1 : 2)
                .ThenBy(v => v.Container.ContainerNumber)
            : query.Sort switch
            {
                // Longest stay first; the number keeps rows with the same time in a stable order
                YardSort.Oldest => visits.OrderBy(v => v.GateInAt).ThenBy(v => v.Container.ContainerNumber),
                YardSort.Newest => visits.OrderByDescending(v => v.GateInAt).ThenBy(v => v.Container.ContainerNumber),
                _ => visits.OrderBy(v => v.Block.Code)
                    .ThenBy(v => v.Row)
                    .ThenBy(v => v.Bay)
                    .ThenBy(v => v.Tier)
                    .ThenBy(v => v.Container.ContainerNumber)
            };

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(YardProjections.ToYardContainer(dbContext))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<YardContainerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
