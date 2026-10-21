using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;

public sealed class GetDeliveryOrderQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetDeliveryOrderQuery, Result<DeliveryOrderResponse>>
{
    public async Task<Result<DeliveryOrderResponse>> HandleAsync(
        GetDeliveryOrderQuery query, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());

        var order = await dbContext.DeliveryOrders
            .AsNoTracking()
            .Where(o => o.Id == query.Id)
            .Select(DeliveryOrderProjection.ToResponse(today))
            .FirstOrDefaultAsync(cancellationToken);

        return order is null
            ? Result.Failure<DeliveryOrderResponse>(Error.NotFound("DeliveryOrder.NotFound",
                $"Delivery order with ID '{query.Id}' was not found."))
            : Result.Success(order);
    }
}
