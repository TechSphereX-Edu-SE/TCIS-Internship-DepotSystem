using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

/*
    Feedback 7 + 8 (Lịch sử container): every visit, open or closed, one row per visit.
      - Main filters: container number (part of it), line operator of the visit, period (From / To, Vietnam
        dates, both included) and the event the period applies to:
          GateIn  = gate-in time in the period, GateOut = gate-out time in the period,
          Any     = at least one of the two in the period (still one row per visit).
      - More: type, size, open / closed, exit purpose.
      - ContainerId: one container only (the "Xem lịch sử" button of the container catalog).
    Newest first by the event looked at: gate-out time for GateOut, gate-in time otherwise.
    This is not a past-date stock report: a gate-in filter over a period is not "what was in the yard then".
*/
public sealed record SearchVisitHistoryQuery(
    string? Search = null,
    Guid? ContainerId = null,
    Guid? OperatorId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    VisitHistoryEvent Event = VisitHistoryEvent.Any,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    VisitState? State = null,
    GateOutPurpose? Purpose = null,
    int Page = 1,
    int PageSize = 20)
    : IQuery<Result<PagedResult<VisitHistoryRow>>>;

public enum VisitHistoryEvent
{
    Any,
    GateIn,
    GateOut
}

// Open = still in the depot (not gated out), Closed = gated out
public enum VisitState
{
    Open,
    Closed
}
