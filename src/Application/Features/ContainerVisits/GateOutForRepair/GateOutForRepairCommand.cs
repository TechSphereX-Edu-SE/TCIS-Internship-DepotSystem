using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOutForRepair;

/*
    Feedback 4/6: the gate-out page confirms that a container requested for outside repair really left
    (chuyển sửa ngoài). No delivery order: its quantities are untouched. The vehicle is recorded as for any gate-out.
*/
public sealed record GateOutForRepairCommand(Guid TransferId, string VehicleNo) : ICommand<Result<GateOutForRepairResponse>>;

public sealed record GateOutForRepairResponse(
    Guid VisitId,
    string ContainerNumber,
    string Facility,
    string VehicleNo,
    DateTimeOffset GateOutAt);
