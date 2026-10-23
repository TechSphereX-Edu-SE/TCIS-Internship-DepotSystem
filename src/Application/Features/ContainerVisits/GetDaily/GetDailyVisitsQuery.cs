using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetDaily;

/*
    Gate-ins and gate-outs of one Vietnam calendar day (default: today in Vietnam).
    Feeds the "today" tables on the gate-in and gate-out screens.
*/
public sealed record GetDailyVisitsQuery(DateOnly? Date = null) : IQuery<Result<DailyVisitsResponse>>;

public sealed record DailyVisitsResponse(
    DateOnly Date,
    IReadOnlyList<DailyGateInResponse> GateIns,
    IReadOnlyList<DailyGateOutResponse> GateOuts);

/*
    Block/Bay/Row/Tier = current position of the visit (it follows the container if it was moved later).
    GateInBlockCode/Bay/Row/Tier = where it was put at gate-in (feedback 3: the table shows the block at gate-in);
    null for visits recorded before the gate-in position was stored.
*/
public sealed record DailyGateInResponse(
    Guid VisitId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    string LineOperatorCode,
    string BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    string? GateInBlockCode,
    int? GateInBay,
    int? GateInRow,
    int? GateInTier,
    string VehicleNo,
    DateTimeOffset GateInAt);

// OrderLineQuantity / OrderLineRemaining: the delivery order line of the container's type, as it is now
public sealed record DailyGateOutResponse(
    Guid VisitId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    string LineOperatorCode,
    string? DeliveryOrderNumber,
    int? OrderLineQuantity,
    int? OrderLineRemaining,
    string? VehicleNo,
    DateTimeOffset GateOutAt);
