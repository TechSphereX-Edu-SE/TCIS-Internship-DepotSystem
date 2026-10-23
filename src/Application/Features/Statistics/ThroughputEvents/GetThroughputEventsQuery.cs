using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.ThroughputEvents;

/*
    Feedback 7: detail rows under the throughput totals, one tab per event kind (Lượt nhập / Lượt xuất).
    Same filters as the totals (IThroughputFilter), so the row count of a tab equals its total.
    Newest event first: gate-in time on the gate-in tab, gate-out time on the gate-out tab.
    A gate-in row can show a gate-out after the period as a reference; it is not a gate-out of this period.
*/
public sealed record GetThroughputEventsQuery(
    ThroughputEventKind Kind,
    DateOnly From,
    DateOnly To,
    Guid? LineOperatorId = null,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    GateOutPurpose? Purpose = null,
    int Page = 1,
    int PageSize = 20)
    : IQuery<Result<PagedResult<VisitHistoryRow>>>, IThroughputFilter;

public enum ThroughputEventKind
{
    GateIn,
    GateOut
}
