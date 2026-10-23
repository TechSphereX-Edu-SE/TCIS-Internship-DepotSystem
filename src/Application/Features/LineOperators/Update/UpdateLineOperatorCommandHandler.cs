using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

/*
    Updates a partner's name, tax code and roles.
    A role cannot be removed while it is in use (409), so no container is left with an owner
    that is "not an owner" and no running operation with an operator that is "not an operator":
      - Owner: the partner owns at least one container.
      - Operator: a pending or confirmed operating assignment, or an open visit, uses the partner.
*/
public sealed class UpdateLineOperatorCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateLineOperatorCommand>
{
    public async Task<Result> HandleAsync(
        UpdateLineOperatorCommand command, CancellationToken cancellationToken = default)
    {
        var partner = await dbContext.LineOperators.FindAsync([command.Id], cancellationToken);
        if (partner is null)
            return Result.Failure(Error.NotFound("LineOperator.NotFound", $"Line operator with ID '{command.Id}' was not found."));

        if (partner.IsOwner && !command.IsOwner &&
            await dbContext.Containers.AnyAsync(c => c.OwnerId == partner.Id, cancellationToken))
            return Result.Failure(Error.Conflict("Partner.OwnerRoleInUse",
                $"{partner.Code} still owns containers; change their owner before removing the owner role."));

        if (partner.IsOperator && !command.IsOperator && await OperatesSomethingAsync(partner.Id, cancellationToken))
            return Result.Failure(Error.Conflict("Partner.OperatorRoleInUse",
                $"{partner.Code} has operating assignments or containers in the yard; end them before removing the operator role."));

        var update = partner.UpdateProfile(command.Name, command.TaxCode, command.IsOwner, command.IsOperator);
        if (update.IsFailure)
            return update;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Cancelled assignments and closed visits are history only, they do not keep the role in use
    private async Task<bool> OperatesSomethingAsync(Guid partnerId, CancellationToken cancellationToken) =>
        await dbContext.OperatingAssignments.AnyAsync(
            a => a.OperatorId == partnerId && a.Status != OperatingAssignmentStatus.Cancelled, cancellationToken) ||
        await dbContext.ContainerVisits.AnyAsync(
            v => v.LineOperatorId == partnerId && v.GateOutAt == null, cancellationToken);
}
