using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;

/*
    Delivery order from a line operator (spec section 3). Fields are exactly what the spec lists:
    customer (tax code and name, via CustomerId), order number, expiry date (last day empty
    containers may be released), outbound vessel/voyage, and the quantity of each container type.
    Q1 (23/10/2026): a line is a type AND a size, e.g. 3 x Dry 20ft + 2 x Dry 40ft.
*/
public sealed record CreateDeliveryOrderCommand(
    string OrderNumber,
    Guid LineOperatorId,
    Guid CustomerId,
    DateOnly ExpiryDate,
    string VesselVoyage,
    IReadOnlyList<DeliveryOrderLineRequest> Lines)
    : ICommand<Result<CreateDeliveryOrderResponse>>;

// Quantity for one container type (Appendix II) and size, e.g. 3 x Dry 20ft; the validator requires the size
public sealed record DeliveryOrderLineRequest(ContainerCategory Category, int Quantity, ContainerSize? Size = null);

public sealed record CreateDeliveryOrderResponse(Guid Id, string OrderNumber);
