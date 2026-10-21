using System.Linq.Expressions;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;

public sealed record GetDeliveryOrderQuery(Guid Id) : IQuery<Result<DeliveryOrderResponse>>;

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
    DateTimeOffset CreatedAt);

// Quantity ordered, already released at gate-out, and still available for one container type
public sealed record DeliveryOrderLineResponse(
    ContainerCategory Category,
    int Quantity,
    int ReleasedQuantity,
    int RemainingQuantity);

/*
    One projection for the get and list queries, so both return the same shape.
    "today" is the Vietnam date: an order expires after its expiry date has passed.
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
                .OrderBy(l => l.Category)
                .Select(l => new DeliveryOrderLineResponse(
                    l.Category, l.Quantity, l.ReleasedQuantity, l.Quantity - l.ReleasedQuantity))
                .ToList(),
            o.CreatedAt);
}
