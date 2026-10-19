using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

public sealed class GetAllContainersQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllContainersQuery, Result<PagedResult<ContainerResponse>>>
{
    public async Task<Result<PagedResult<ContainerResponse>>> HandleAsync(
        GetAllContainersQuery query, CancellationToken cancellationToken = default)
    {
        var totalCount = await dbContext.Containers.CountAsync(cancellationToken);

        var containers = await dbContext.Containers
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = containers.Select(ContainerResponse.From).ToList();

        return Result.Success(new PagedResult<ContainerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
