using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;

// Newest orders first, same shape as the single-order query
public sealed class GetAllDeliveryOrdersQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetAllDeliveryOrdersQuery, Result<PagedResult<DeliveryOrderResponse>>>
{
    public async Task<Result<PagedResult<DeliveryOrderResponse>>> HandleAsync(
        GetAllDeliveryOrdersQuery query, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        var totalCount = await dbContext.DeliveryOrders.CountAsync(cancellationToken);

        var items = await dbContext.DeliveryOrders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.OrderNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(DeliveryOrderProjection.ToResponse(today))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<DeliveryOrderResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
