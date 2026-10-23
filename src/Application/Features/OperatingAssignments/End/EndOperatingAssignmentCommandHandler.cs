using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.End;

/*
    Order of checks:
      1. The assignment exists (404) and is confirmed (409, domain); the end is not before the start (400, domain).
      2. No visit based on it came in after the new end date (409): the visit's basis must keep covering
         its gate-in date, history is not rewritten (Q5).
    A container in the yard keeps its operator (Q5); a new operator only applies to the next gate-in.
*/
public sealed class EndOperatingAssignmentCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<EndOperatingAssignmentCommand>
{
    public async Task<Result> HandleAsync(EndOperatingAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.OperatingAssignments.FindAsync([command.Id], cancellationToken);
        if (assignment is null)
            return Result.Failure(OperatingAssignmentQueries.NotFound(command.Id));

        // First instant after the new end date, in Vietnam time
        var afterEnd = DepotTime.StartOf(command.ValidTo.AddDays(1));
        var usedLater = await dbContext.ContainerVisits.AnyAsync(
            v => v.OperatingAssignmentId == assignment.Id && v.GateInAt >= afterEnd, cancellationToken);
        if (usedLater)
            return Result.Failure(Error.Conflict("OperatingAssignment.UsedAfterEnd",
                $"A gate-in after {command.ValidTo:dd/MM/yyyy} is based on this assignment; choose a later end date."));

        var end = assignment.End(command.ValidTo, timeProvider.GetUtcNow());
        if (end.IsFailure)
            return end;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
