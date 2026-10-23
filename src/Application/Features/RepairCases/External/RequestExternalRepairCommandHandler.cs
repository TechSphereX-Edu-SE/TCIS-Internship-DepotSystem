using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

/*
    Order of checks:
      1. The case exists (404), is open and approved for repair or in repair (409, domain).
      2. The case has no other transfer waiting to leave or at a facility (409 ExternalRepair.AlreadyActive);
         the filtered unique index also guards two requests at the same moment.
      3. The container is in the yard (409 ExternalRepair.NotInYard): only a container at the depot can leave.
*/
public sealed class RequestExternalRepairCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<RequestExternalRepairCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(RequestExternalRepairCommand command, CancellationToken cancellationToken = default)
    {
        var repairCase = await dbContext.RepairCases.FindAsync([command.CaseId], cancellationToken);
        if (repairCase is null)
            return Result.Failure<Guid>(ApplyRepairActionCommandHandler.NotFound(command.CaseId));

        var active = await dbContext.ExternalRepairTransfers.AnyAsync(
            t => t.RepairCaseId == repairCase.Id &&
                 (t.Status == ExternalRepairStatus.Requested || t.Status == ExternalRepairStatus.GatedOut),
            cancellationToken);
        if (active)
            return Result.Failure<Guid>(Error.Conflict("ExternalRepair.AlreadyActive",
                "The case already has an external repair transfer waiting to leave or at a facility."));

        var inYard = await dbContext.ContainerVisits.AnyAsync(
            v => v.ContainerId == repairCase.ContainerId && v.GateOutAt == null, cancellationToken);
        if (!inYard)
            return Result.Failure<Guid>(Error.Conflict("ExternalRepair.NotInYard",
                "The container is not in the yard, so it cannot be sent out."));

        var requested = ExternalRepairTransfer.Request(repairCase, command.Facility, command.Reason, command.Reference,
            timeProvider.GetUtcNow(), currentUser.Email);
        if (requested.IsFailure)
            return Result.Failure<Guid>(requested.Error!);

        var (transfer, history) = requested.Value;
        dbContext.ExternalRepairTransfers.Add(transfer);
        dbContext.RepairCaseEvents.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(transfer.Id);
    }
}
