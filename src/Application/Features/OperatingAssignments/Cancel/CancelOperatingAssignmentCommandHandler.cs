using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Cancel;

/*
    404 unknown; 409 when already cancelled (domain) or when a visit took its operator from it:
    cancelling would leave that visit without its basis. End it with a date instead.
*/
public sealed class CancelOperatingAssignmentCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CancelOperatingAssignmentCommand>
{
    public async Task<Result> HandleAsync(CancelOperatingAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.OperatingAssignments.FindAsync([command.Id], cancellationToken);
        if (assignment is null)
            return Result.Failure(OperatingAssignmentQueries.NotFound(command.Id));

        if (await dbContext.ContainerVisits.AnyAsync(v => v.OperatingAssignmentId == assignment.Id, cancellationToken))
            return Result.Failure(Error.Conflict("OperatingAssignment.InUse",
                "A visit is based on this assignment, so it cannot be cancelled. Set an end date instead."));

        var cancel = assignment.Cancel(command.Reason, timeProvider.GetUtcNow());
        if (cancel.IsFailure)
            return cancel;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
