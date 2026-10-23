using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Cancel;

/*
    The order is kept (history), only marked cancelled: it leaves the gate-out dropdown and can
    no longer release a container. 409 DeliveryOrder.HasReleases once a container left under it.
    A gate-out racing with the cancel: the line row version makes one of them fail (409 Data.ConcurrentUpdate).
*/
public sealed class CancelDeliveryOrderCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CancelDeliveryOrderCommand>
{
    public async Task<Result> HandleAsync(CancelDeliveryOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.DeliveryOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (order is null)
            return Result.Failure(Error.NotFound("DeliveryOrder.NotFound", $"Delivery order with ID '{command.Id}' was not found."));

        var cancelled = order.Cancel(command.Reason, timeProvider.GetUtcNow());
        if (cancelled.IsFailure)
            return cancelled;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
