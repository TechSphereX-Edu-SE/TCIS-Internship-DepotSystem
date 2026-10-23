using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;

/*
    Gate-out for empty release (cấp rỗng): ends a container lifecycle (spec section 3).
    The depot only releases a container under a delivery order of its line operator,
    and records the vehicle taking it out. The gate-out time comes from the server clock.
    The transfer to an outside repair facility has its own command (GateOutForRepairCommand).
*/
public sealed record GateOutCommand(
    Guid ContainerId,
    Guid DeliveryOrderId,
    string VehicleNo)
    : ICommand<Result<GateOutResponse>>;

// RemainingQuantity: containers of this type and size still allowed under the order after this release
public sealed record GateOutResponse(
    Guid VisitId,
    string ContainerNumber,
    string OrderNumber,
    ContainerCategory Category,
    int RemainingQuantity,
    string VehicleNo,
    DateTimeOffset GateOutAt,
    ContainerSize Size);
