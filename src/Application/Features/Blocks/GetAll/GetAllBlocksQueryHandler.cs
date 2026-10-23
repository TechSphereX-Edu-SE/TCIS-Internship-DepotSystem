using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;

public sealed class GetAllBlocksQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllBlocksQuery, Result<PagedResult<BlockResponse>>>
{
    public async Task<Result<PagedResult<BlockResponse>>> HandleAsync(
        GetAllBlocksQuery query, CancellationToken cancellationToken = default)
    {
        var blocks = dbContext.Blocks.AsNoTracking();
        if (!query.IncludeArchived)
            blocks = blocks.Where(b => b.ArchivedAt == null);

        var totalCount = await blocks.CountAsync(cancellationToken);

        // Active blocks first, then archived ones; by code inside each group
        var items = await blocks
            .OrderBy(b => b.ArchivedAt != null)
            .ThenBy(b => b.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BlockResponse(
                b.Id, b.Code, b.Description, b.IsVirtual, b.MaxBays, b.MaxRows, b.MaxTiers, b.AllowedCategories, b.ArchivedAt,
                dbContext.ContainerVisits.Count(v => v.BlockId == b.Id && v.GateOutAt == null),
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BlockResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
