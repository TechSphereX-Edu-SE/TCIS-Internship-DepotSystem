using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

public sealed class GetAllContainersQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetAllContainersQuery, Result<PagedResult<ContainerResponse>>>
{
    public async Task<Result<PagedResult<ContainerResponse>>> HandleAsync(
        GetAllContainersQuery query, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        var containers = dbContext.Containers.AsNoTracking().Include(c => c.Owner).AsQueryable();
        IOrderedQueryable<Container> ordered;

        if (query.OperatorId is { } operatorId)
        {
            var operated = dbContext.OperatingAssignments.ActiveOn(today)
                .Where(a => a.OperatorId == operatorId)
                .Select(a => a.ContainerId);
            containers = containers.Where(c => operated.Contains(c.Id));
        }

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            // Plain list: newest first; the number breaks ties so paging is stable
            ordered = containers.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.ContainerNumber);
        }
        else
        {
            // Numbers are stored normalized (trim, upper case), so normalize the search text the same way
            var search = ContainerNumberRules.Normalize(query.Search);
            containers = containers.Where(c => c.ContainerNumber.Contains(search));

            // Feedback 1: exact match first, then numbers starting with the text, then the rest
            // (e.g. typing the serial digits). Ranked in SQL (CASE) before paging, so page 1 holds the best matches.
            ordered = containers
                .OrderBy(c => c.ContainerNumber == search ? 0 : c.ContainerNumber.StartsWith(search) ? 1 : 2)
                .ThenBy(c => c.ContainerNumber);
        }

        var totalCount = await containers.CountAsync(cancellationToken);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Today's operator of the containers on this page, in one query
        var ids = page.Select(c => c.Id).ToList();
        var operators = await dbContext.OperatingAssignments.AsNoTracking()
            .Include(a => a.Operator)
            .Where(a => ids.Contains(a.ContainerId))
            .ActiveOn(today)
            .ToListAsync(cancellationToken);
        var operatorByContainer = operators
            .GroupBy(a => a.ContainerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ValidFrom).First().Operator);

        var items = page.Select(c => ContainerResponse.From(c, operatorByContainer.GetValueOrDefault(c.Id))).ToList();

        return Result.Success(new PagedResult<ContainerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
