using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;

public sealed class GetBlockQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetBlockQuery, Result<BlockResponse>>
{
    public async Task<Result<BlockResponse>> HandleAsync(
        GetBlockQuery query, CancellationToken cancellationToken = default)
    {
        var block = await dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.Id == query.Id)
            .Select(b => new BlockResponse(
                b.Id, b.Code, b.Description, b.IsVirtual, b.MaxBays, b.MaxRows, b.MaxTiers,
                // Containers currently in this block (open visits only)
                dbContext.ContainerVisits.Count(v => v.BlockId == b.Id && v.GateOutAt == null),
                b.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return block is null
            ? Result.Failure<BlockResponse>(Error.NotFound("Block.NotFound", $"Block with ID '{query.Id}' was not found."))
            : Result.Success(block);
    }
}
