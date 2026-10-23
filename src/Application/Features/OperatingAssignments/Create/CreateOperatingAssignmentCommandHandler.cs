using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Create;

/*
    Order of checks:
      1. Container and partner exist (404); the partner has the Operator role (409, domain).
      2. When confirmed at once: no other confirmed assignment of the container overlaps the period (409).
    The owner of the container is not involved: an operator may rent a container owned by another company.
*/
public sealed class CreateOperatingAssignmentCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CreateOperatingAssignmentCommand, Result<CreateOperatingAssignmentResponse>>
{
    public async Task<Result<CreateOperatingAssignmentResponse>> HandleAsync(
        CreateOperatingAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.ContainerId], cancellationToken);
        if (container is null)
            return Result.Failure<CreateOperatingAssignmentResponse>(Error.NotFound("Container.NotFound",
                $"Container with ID '{command.ContainerId}' was not found."));

        var lineOperator = await dbContext.LineOperators.FindAsync([command.OperatorId], cancellationToken);
        if (lineOperator is null)
            return Result.Failure<CreateOperatingAssignmentResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{command.OperatorId}' was not found."));

        var created = OperatingAssignment.Create(
            container, lineOperator, command.ValidFrom, command.ValidTo, command.Reference, command.Note);
        if (created.IsFailure)
            return Result.Failure<CreateOperatingAssignmentResponse>(created.Error!);

        var assignment = created.Value!;
        if (command.Confirm)
        {
            var overlap = await dbContext.CheckNoConfirmedOverlapAsync(assignment, cancellationToken);
            if (overlap.IsFailure)
                return Result.Failure<CreateOperatingAssignmentResponse>(overlap.Error!);

            assignment.Confirm(timeProvider.GetUtcNow());
        }

        dbContext.OperatingAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(new CreateOperatingAssignmentResponse(assignment.Id));
    }
}
