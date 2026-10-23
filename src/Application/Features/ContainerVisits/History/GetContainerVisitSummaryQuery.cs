using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

/*
    Feedback 7: summary of one container over all its visits, shown above its history rows.
      - GateInCount / GateOutCount: one per visit, a container that came back is never merged.
      - Presence: where it is now (never came in / in the depot / at an outside repair facility / out).
      - CurrentDwellDays: days of the open visit (null when out).
      - TotalDwellDays: sum of the dwell days of every visit (Vietnam calendar days, 0 on the gate-in day);
        time outside the depot between two visits is not added.
    Owner: the current owner of the catalog (the rows show the owner at each gate-in).
*/
public sealed record GetContainerVisitSummaryQuery(Guid ContainerId) : IQuery<Result<ContainerVisitSummary>>;

public sealed record ContainerVisitSummary(
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerCategory Category,
    ContainerSize Size,
    string? OwnerCode,
    string? OwnerName,
    int GateInCount,
    int GateOutCount,
    ContainerPresence Presence,
    Guid? CurrentVisitId,
    int? CurrentDwellDays,
    int TotalDwellDays,
    DateTimeOffset AsOf);

public enum ContainerPresence
{
    NeverIn,
    InDepot,
    AtOutsideRepair,
    OutOfDepot
}
