using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;

// Newest orders first (or best search match first), same shape as the single-order query
public sealed class GetAllDeliveryOrdersQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetAllDeliveryOrdersQuery, Result<PagedResult<DeliveryOrderResponse>>>
{
    public async Task<Result<PagedResult<DeliveryOrderResponse>>> HandleAsync(
        GetAllDeliveryOrdersQuery query, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        var orders = dbContext.DeliveryOrders.AsNoTracking();

        if (query.OperatorId is { } operatorId)
            orders = orders.Where(o => o.LineOperatorId == operatorId);

        if (query.Usable)
            orders = orders.Where(o => o.CancelledAt == null && o.ExpiryDate >= today &&
                                       o.Lines.Any(l => l.Quantity > l.ReleasedQuantity));

        if (query.Status is { } status)
            orders = WithStatus(orders, status, today);

        IOrderedQueryable<DeliveryOrder> ordered;
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            ordered = orders.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.OrderNumber);
        }
        else
        {
            // Order numbers are stored upper case; customer names are compared in lower case
            var number = query.Search.Trim().ToUpperInvariant();
            var name = query.Search.Trim().ToLower();
            orders = orders.Where(o => o.OrderNumber.Contains(number) || o.Customer.Name.ToLower().Contains(name));

            // Feedback 1: exact number first, then numbers starting with the text, then the other matches
            ordered = orders
                .OrderBy(o => o.OrderNumber == number ? 0 : o.OrderNumber.StartsWith(number) ? 1 : 2)
                .ThenByDescending(o => o.CreatedAt)
                .ThenBy(o => o.OrderNumber);
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(DeliveryOrderProjection.ToResponse(today))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<DeliveryOrderResponse>(items, totalCount, query.Page, query.PageSize));
    }

    // Same rule as DeliveryOrder.StatusOf, written as SQL conditions
    private static IQueryable<DeliveryOrder> WithStatus(IQueryable<DeliveryOrder> orders, DeliveryOrderStatus status, DateOnly today)
    {
        var open = orders.Where(o => o.CancelledAt == null && o.Lines.Any(l => l.Quantity > l.ReleasedQuantity));
        return status switch
        {
            DeliveryOrderStatus.Cancelled => orders.Where(o => o.CancelledAt != null),
            DeliveryOrderStatus.Completed => orders.Where(o => o.CancelledAt == null && !o.Lines.Any(l => l.Quantity > l.ReleasedQuantity)),
            DeliveryOrderStatus.Expired => open.Where(o => o.ExpiryDate < today),
            DeliveryOrderStatus.NotStarted => open.Where(o => o.ExpiryDate >= today && !o.Lines.Any(l => l.ReleasedQuantity > 0)),
            DeliveryOrderStatus.InProgress => open.Where(o => o.ExpiryDate >= today && o.Lines.Any(l => l.ReleasedQuantity > 0)),
            _ => orders
        };
    }
}
