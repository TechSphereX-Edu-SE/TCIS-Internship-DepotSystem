using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateStatus;

public sealed class UpdateContainerStatusCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateContainerStatusCommand>
{
    public async Task<Result> HandleAsync(
        UpdateContainerStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Status))
            return Result.Failure(Error.Validation("Container.InvalidStatus", "Invalid container status."));

        var container = await dbContext.Containers.FindAsync([command.Id], cancellationToken);
        if (container is null)
            return Result.Failure(Error.NotFound("Container.NotFound", $"Container with ID '{command.Id}' was not found."));

        switch (command.Status)
        {
            case ContainerStatus.InYard: container.ReturnToYard(); break;
            case ContainerStatus.Loaded: container.MarkAsLoaded(); break;
            case ContainerStatus.Departed: container.MarkAsDeparted(); break;
            case ContainerStatus.Damaged: container.MarkAsDamaged(); break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
