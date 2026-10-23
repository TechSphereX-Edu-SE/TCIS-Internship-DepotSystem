using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOutForRepair;

/*
    Order of checks:
      1. The transfer exists (404) and still waits to leave (409 ExternalRepair.NotRequested).
      2. The container is in the yard (404 NotInYard).
      3. Nothing is stacked on it (409 ContainerVisit.ContainerAbove): the lifting rules stay the same.
    Then the visit is closed (position freed, the container leaves the stock) with purpose ExternalRepair,
    the transfer becomes GatedOut and the case history records it. The container stays Damaged and
    its repair case stays open; the next gate-in of the container is its return.
*/
public sealed class GateOutForRepairCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<GateOutForRepairCommand, Result<GateOutForRepairResponse>>
{
    public async Task<Result<GateOutForRepairResponse>> HandleAsync(
        GateOutForRepairCommand command, CancellationToken cancellationToken = default)
    {
        var transfer = await dbContext.ExternalRepairTransfers
            .Include(t => t.RepairCase)
            .FirstOrDefaultAsync(t => t.Id == command.TransferId, cancellationToken);
        if (transfer is null)
            return Result.Failure<GateOutForRepairResponse>(CancelExternalRepairCommandHandler.NotFound(command.TransferId));

        if (transfer.Status != ExternalRepairStatus.Requested)
            return Result.Failure<GateOutForRepairResponse>(ExternalRepairTransfer.NotRequested());

        var visit = await dbContext.FindOpenVisitAsync(transfer.ContainerId, cancellationToken);
        if (visit is null)
            return Result.Failure<GateOutForRepairResponse>(await dbContext.NotInYardErrorAsync(transfer.ContainerId, cancellationToken));

        var leave = await dbContext.CheckNothingAboveAsync(visit, visit.Container.Size, cancellationToken);
        if (leave.IsFailure)
            return Result.Failure<GateOutForRepairResponse>(leave.Error!);

        var now = timeProvider.GetUtcNow();
        var gateOut = visit.GateOutForRepair(command.VehicleNo, now);
        if (gateOut.IsFailure)
            return Result.Failure<GateOutForRepairResponse>(gateOut.Error!);

        var marked = transfer.MarkGatedOut(transfer.RepairCase, visit, now, currentUser.Email);
        if (marked.IsFailure)
            return Result.Failure<GateOutForRepairResponse>(marked.Error!);

        dbContext.RepairCaseEvents.Add(marked.Value!);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new GateOutForRepairResponse(
            visit.Id, visit.Container.ContainerNumber, transfer.Facility, visit.GateOutVehicleNo!, now));
    }
}
