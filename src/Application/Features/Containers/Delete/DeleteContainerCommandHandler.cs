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

        dbContext.Containers.Remove(container);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
