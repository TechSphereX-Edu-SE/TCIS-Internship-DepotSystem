using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Delete;

public sealed class DeleteContainerCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<DeleteContainerCommand>
{
    public async Task<Result> HandleAsync(
        DeleteContainerCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.Id], cancellationToken);
        if (container is null)
            return Result.Failure(Error.NotFound("Container.NotFound", $"Container with ID '{command.Id}' was not found."));

        // Keep containers that have been in the yard so history and statistics are not lost
        var hasHistory = await dbContext.ContainerVisits
            .AnyAsync(v => v.ContainerId == command.Id, cancellationToken);
        if (hasHistory)
            return Result.Failure(Error.Conflict("Container.HasHistory",
                $"Container '{container.ContainerNumber}' has gate-in/out history and cannot be deleted."));

        dbContext.Containers.Remove(container);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
