using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Confirm;

// 404 unknown; 409 when it is not pending (domain) or overlaps another confirmed assignment
public sealed class ConfirmOperatingAssignmentCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<ConfirmOperatingAssignmentCommand>
{
    public async Task<Result> HandleAsync(ConfirmOperatingAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        var assignment = await dbContext.OperatingAssignments.FindAsync([command.Id], cancellationToken);
        if (assignment is null)
            return Result.Failure(OperatingAssignmentQueries.NotFound(command.Id));

        var overlap = await dbContext.CheckNoConfirmedOverlapAsync(assignment, cancellationToken);
        if (overlap.IsFailure)
            return overlap;

        var confirm = assignment.Confirm(timeProvider.GetUtcNow());
        if (confirm.IsFailure)
            return confirm;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
