using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Report;

/*
    The finding is linked to the open visit when the container is in the yard; a container outside
    the depot can still get a report (e.g. found on its master data), without a visit.
    Two reports racing to open a case for the same container: the unique index on open cases
    turns the second into 409 Data.ConcurrentUpdate (reload, the report then joins the case).
*/
public sealed class ReportDamageCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<ReportDamageCommand, Result<RecordedDamage>>
{
    public async Task<Result<RecordedDamage>> HandleAsync(ReportDamageCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.ContainerId], cancellationToken);
        if (container is null)
            return Result.Failure<RecordedDamage>(Error.NotFound("Container.NotFound",
                $"Container with ID '{command.ContainerId}' was not found."));

        var visit = await dbContext.FindOpenVisitAsync(container.Id, cancellationToken);
        var recorded = await dbContext.RecordDamageAsync(
            container, visit?.Id, command.Description, command.Source,
            timeProvider.GetUtcNow(), currentUser.Email, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(recorded);
    }
}
