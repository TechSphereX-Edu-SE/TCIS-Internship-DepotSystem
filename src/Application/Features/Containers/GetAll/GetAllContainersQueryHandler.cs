using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

public sealed class GetAllContainersQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllContainersQuery, Result<PagedResult<ContainerResponse>>>
{
    public async Task<Result<PagedResult<ContainerResponse>>> HandleAsync(
        GetAllContainersQuery query, CancellationToken cancellationToken = default)
    {
        var containers = dbContext.Containers.AsNoTracking();

        // Numbers are stored normalized (trim, upper case), so normalize the search text the same way
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = ContainerNumberRules.Normalize(query.Search);
            containers = containers.Where(c => c.ContainerNumber.Contains(search));
        }

        var totalCount = await containers.CountAsync(cancellationToken);

        var page = await containers
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(ContainerResponse.From).ToList();

        return Result.Success(new PagedResult<ContainerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
