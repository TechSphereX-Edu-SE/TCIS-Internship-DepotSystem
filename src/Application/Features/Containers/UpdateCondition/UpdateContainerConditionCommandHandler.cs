using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;

// The domain keeps the note only for a damaged container (Container.UpdateCondition)
public sealed class UpdateContainerConditionCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateContainerConditionCommand>
{
    public async Task<Result> HandleAsync(UpdateContainerConditionCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.Id], cancellationToken);
        if (container is null)
            return Result.Failure(Error.NotFound("Container.NotFound", $"Container with ID '{command.Id}' was not found."));

        container.UpdateCondition(command.Condition, command.ConditionNote);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
