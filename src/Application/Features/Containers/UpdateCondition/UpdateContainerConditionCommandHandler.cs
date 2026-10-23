using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;

/*
    Feedback 4: the condition is no longer a switch that opens release.
      - Damaged: recorded as a finding of a repair case (opened when there is none), like any damage report.
      - Normal: refused while a repair case is open (409 Container.RepairCaseOpen); the case is passed
        on the repair page instead. Without an open case (older data) it simply clears the flag.
*/
public sealed class UpdateContainerConditionCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<UpdateContainerConditionCommand>
{
    public async Task<Result> HandleAsync(UpdateContainerConditionCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.Id], cancellationToken);
        if (container is null)
            return Result.Failure(Error.NotFound("Container.NotFound", $"Container with ID '{command.Id}' was not found."));

        if (command.Condition == ContainerCondition.Damaged)
        {
            var visit = await dbContext.FindOpenVisitAsync(container.Id, cancellationToken);
            var description = string.IsNullOrWhiteSpace(command.ConditionNote) ? "Damaged (no description)" : command.ConditionNote;
            await dbContext.RecordDamageAsync(container, visit?.Id, description, RepairFindingSource.Catalog,
                timeProvider.GetUtcNow(), null, cancellationToken);
        }
        else
        {
            if (await dbContext.FindOpenCaseAsync(container.Id, cancellationToken) is not null)
                return Result.Failure(Error.Conflict("Container.RepairCaseOpen",
                    $"Container {container.ContainerNumber} has an open inspection / repair case; pass it on the repair page."));

            container.UpdateCondition(command.Condition, command.ConditionNote);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
