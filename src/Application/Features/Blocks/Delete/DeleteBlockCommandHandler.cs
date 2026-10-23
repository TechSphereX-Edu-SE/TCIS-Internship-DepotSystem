using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Delete;

/*
    Order of checks:
      1. The block exists (404) and is not archived yet (409, domain).
      2. No container is in this block now (409) - only this block, the rest of the depot does not matter.
         Containers are never moved or deleted automatically.
      3. A block used by any visit (current block or block at gate-in) is archived: old visits keep
         showing its code, and it disappears from the gate-in / move dropdowns. Otherwise it is removed.
    A gate-in or move into this block at the same moment changes the block row (PlacementVersion),
    so one of the two saves fails with a concurrency conflict (409) instead of leaving a container
    in a deleted block.
*/
public sealed class DeleteBlockCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<DeleteBlockCommand, Result<DeleteBlockResponse>>
{
    public async Task<Result<DeleteBlockResponse>> HandleAsync(
        DeleteBlockCommand command, CancellationToken cancellationToken = default)
    {
        var block = await dbContext.Blocks.FindAsync([command.Id], cancellationToken);
        if (block is null)
            return Result.Failure<DeleteBlockResponse>(Error.NotFound("Block.NotFound",
                $"Block with ID '{command.Id}' was not found."));

        var inBlock = await dbContext.ContainerVisits.CountAsync(
            v => v.BlockId == block.Id && v.GateOutAt == null, cancellationToken);
        if (inBlock > 0)
            return Result.Failure<DeleteBlockResponse>(Error.Conflict("Block.HasContainers",
                $"{inBlock} container(s) are in block {block.Code}. Move them out before deleting it."));

        var hasHistory = await dbContext.ContainerVisits.AnyAsync(
            v => v.BlockId == block.Id || v.GateInBlockId == block.Id, cancellationToken);

        if (hasHistory)
        {
            var archive = block.Archive(timeProvider.GetUtcNow());
            if (archive.IsFailure)
                return Result.Failure<DeleteBlockResponse>(archive.Error!);
        }
        else
        {
            dbContext.Blocks.Remove(block);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new DeleteBlockResponse(block.Id, block.Code, hasHistory));
    }
}
