using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;

public sealed class ResizeBlockCommandHandler(IAppDbContext dbContext) : ICommandHandler<ResizeBlockCommand>
{
    public async Task<Result> HandleAsync(ResizeBlockCommand command, CancellationToken cancellationToken = default)
    {
        var block = await dbContext.Blocks.FindAsync([command.Id], cancellationToken);
        if (block is null)
            return Result.Failure(Error.NotFound("Block.NotFound", $"Block with ID '{command.Id}' was not found."));

        // Shrinking must not leave a container in the yard outside the new size.
        // A 40ft container in even bay b also covers bay b+1 ("2 odd bays = 1 even bay").
        var outside = await dbContext.ContainerVisits.CountAsync(v =>
            v.BlockId == block.Id &&
            v.GateOutAt == null &&
            (v.Bay > command.MaxBays ||
             (v.Container.Size == ContainerSize.Ft40 && v.Bay + 1 > command.MaxBays) ||
             v.Row > command.MaxRows ||
             v.Tier > command.MaxTiers),
            cancellationToken);

        if (outside > 0)
            return Result.Failure(Error.Conflict("Block.ContainersOutsideNewSize",
                $"{outside} container(s) in block {block.Code} are outside the new size. Move them first."));

        var resize = block.Resize(command.MaxBays, command.MaxRows, command.MaxTiers);
        if (resize.IsFailure)
            return resize;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
