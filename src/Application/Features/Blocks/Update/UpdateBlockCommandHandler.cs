using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Update;

/*
    Order of checks:
      1. The block exists (404) and is not archived (409, domain).
      2. The new code is not used by another block, archived ones included (409) - codes stay
         unambiguous in the history.
      3. Removing a type from the list is refused while a container of that type is in the block (409):
         the list would no longer describe what is really there. Move those containers first.
    The block keeps its Id, so visits and reports follow the new code without any data change.
*/
public sealed class UpdateBlockCommandHandler(IAppDbContext dbContext) : ICommandHandler<UpdateBlockCommand>
{
    public async Task<Result> HandleAsync(UpdateBlockCommand command, CancellationToken cancellationToken = default)
    {
        var block = await dbContext.Blocks.FindAsync([command.Id], cancellationToken);
        if (block is null)
            return Result.Failure(Error.NotFound("Block.NotFound", $"Block with ID '{command.Id}' was not found."));

        var code = command.Code.Trim().ToUpperInvariant();
        var codeTaken = await dbContext.Blocks.AnyAsync(b => b.Code == code && b.Id != block.Id, cancellationToken);
        if (codeTaken)
            return Result.Failure(Error.Conflict("Block.Duplicate", $"Block '{code}' already exists."));

        // Types of the containers in the block now; read as a small list and compared in memory
        var typesInBlock = await dbContext.ContainerVisits
            .Where(v => v.BlockId == block.Id && v.GateOutAt == null)
            .Select(v => v.Container.Category)
            .Distinct()
            .ToListAsync(cancellationToken);
        var stillNeeded = typesInBlock.Where(c => !command.AllowedCategories.Contains(c)).ToList();
        if (stillNeeded.Count > 0)
            return Result.Failure(Error.Conflict("Block.ContainersOfRemovedType",
                $"Block {block.Code} still holds {string.Join(", ", stillNeeded)} containers. Move them before removing the type."));

        var details = block.UpdateDetails(code, command.Description);
        if (details.IsFailure)
            return details;

        var categories = block.SetAllowedCategories(command.AllowedCategories);
        if (categories.IsFailure)
            return categories;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
