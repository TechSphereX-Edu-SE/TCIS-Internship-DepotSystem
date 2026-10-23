using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

public sealed class GetExternalRepairsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetExternalRepairsQuery, Result<PagedResult<ExternalRepairResponse>>>
{
    public async Task<Result<PagedResult<ExternalRepairResponse>>> HandleAsync(
        GetExternalRepairsQuery query, CancellationToken cancellationToken = default)
    {
        var transfers = dbContext.ExternalRepairTransfers.AsNoTracking();

        if (query.Status is { } status)
            transfers = transfers.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = ContainerNumberRules.Normalize(query.Search);
            transfers = transfers.Where(t => t.Container.ContainerNumber.Contains(search));
        }

        var totalCount = await transfers.CountAsync(cancellationToken);

        var items = await transfers
            .OrderByDescending(t => t.RequestedAt).ThenBy(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ExternalRepairProjection.ToResponse(dbContext.ContainerVisits))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ExternalRepairResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
