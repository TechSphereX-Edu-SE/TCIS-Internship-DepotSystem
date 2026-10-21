using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;

/*
    Releases a container from the yard under a delivery order.
    Order of checks:
      1. The container is in the yard (404) and the delivery order exists (404).
      2. The container is not damaged (409) - project decision: a damaged container is repaired
         and its condition set back to Normal before it is released.
      3. Nothing is stacked on the container (409) - the one above must be moved first.
      4. Domain (ContainerVisit.GateOut / DeliveryOrder.Release), all 409:
         order of the same line operator, not expired (Vietnam date), has a line for the
         container type, and quantity left on that line.
    The CHECK on DeliveryOrderLines (ReleasedQuantity <= Quantity) still guards the database
    if two gate-outs take the last unit at the same time.
*/
public sealed class GateOutCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<GateOutCommand, Result<GateOutResponse>>
{
    public async Task<Result<GateOutResponse>> HandleAsync(
        GateOutCommand command, CancellationToken cancellationToken = default)
    {
        var visit = await dbContext.FindOpenVisitAsync(command.ContainerId, cancellationToken);
        if (visit is null)
            return Result.Failure<GateOutResponse>(await dbContext.NotInYardErrorAsync(command.ContainerId, cancellationToken));

        var order = await dbContext.DeliveryOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == command.DeliveryOrderId, cancellationToken);
        if (order is null)
            return Result.Failure<GateOutResponse>(Error.NotFound("DeliveryOrder.NotFound",
                $"Delivery order with ID '{command.DeliveryOrderId}' was not found."));

        var container = visit.Container;
        if (container.Condition == ContainerCondition.Damaged)
            return Result.Failure<GateOutResponse>(Error.Conflict("ContainerVisit.ContainerDamaged",
                $"Container {container.ContainerNumber} is damaged. Set its condition to Normal after repair before releasing it."));

        var leave = await dbContext.CheckNothingAboveAsync(visit, container.Size, cancellationToken);
        if (leave.IsFailure)
            return Result.Failure<GateOutResponse>(leave.Error!);

        var gateOut = visit.GateOut(order, container.Category, command.VehicleNo, timeProvider.GetUtcNow());
        if (gateOut.IsFailure)
            return Result.Failure<GateOutResponse>(gateOut.Error!);

        await dbContext.SaveChangesAsync(cancellationToken);

        var remaining = order.Lines.Single(l => l.Category == container.Category).RemainingQuantity;
        return Result.Success(new GateOutResponse(
            visit.Id, container.ContainerNumber, order.OrderNumber, container.Category,
            remaining, visit.GateOutVehicleNo!, visit.GateOutAt!.Value));
    }
}
