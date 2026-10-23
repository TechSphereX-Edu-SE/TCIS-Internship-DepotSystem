using System.Linq.Expressions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;

public sealed record GetDeliveryOrderQuery(Guid Id) : IQuery<Result<DeliveryOrderResponse>>;

/*
    Status: Chưa giao / Đang giao / Đã giao đủ / Hết hạn / Đã huỷ (feedback 6, Q7), see DeliveryOrderStatus.
    IsExpired is kept for the screens that only need the date check.
*/
public sealed record DeliveryOrderResponse(
    Guid Id,
    string OrderNumber,
    Guid LineOperatorId,
    string LineOperatorCode,
    Guid CustomerId,
    string CustomerTaxCode,
    string CustomerName,
    DateOnly ExpiryDate,
    bool IsExpired,
    string VesselVoyage,
    List<DeliveryOrderLineResponse> Lines,
    DateTimeOffset CreatedAt,
    DeliveryOrderStatus Status,
    DateTimeOffset? CancelledAt,
    string? CancelReason);

// Quantity ordered, already released at gate-out, and still available for one type and size (Size null = both sizes)
public sealed record DeliveryOrderLineResponse(
    ContainerCategory Category,
    int Quantity,
    int ReleasedQuantity,
    int RemainingQuantity,
    ContainerSize? Size);

/*
    One projection for the get and list queries, so both return the same shape.
    "today" is the Vietnam date: an order expires after its expiry date has passed.
    The status is worked out from the sums of the lines with the same rule as the domain (DeliveryOrder.StatusOf).
*/
public static class DeliveryOrderProjection
{
    public static Expression<Func<DeliveryOrder, DeliveryOrderResponse>> ToResponse(DateOnly today) =>
        o => new DeliveryOrderResponse(
            o.Id,
            o.OrderNumber,
            o.LineOperatorId,
            o.LineOperator.Code,
            o.CustomerId,
            o.Customer.TaxCode,
            o.Customer.Name,
            o.ExpiryDate,
            today > o.ExpiryDate,
            o.VesselVoyage,
            o.Lines
                .OrderBy(l => l.Category).ThenBy(l => l.Size)
                .Select(l => new DeliveryOrderLineResponse(
                    l.Category, l.Quantity, l.ReleasedQuantity, l.Quantity - l.ReleasedQuantity, l.Size))
                .ToList(),
            o.CreatedAt,
            DeliveryOrder.StatusOf(
                o.CancelledAt != null,
                o.Lines.Sum(l => l.ReleasedQuantity),
                o.Lines.Sum(l => l.Quantity - l.ReleasedQuantity),
                o.ExpiryDate,
                today),
            o.CancelledAt,
            o.CancelReason);
}
