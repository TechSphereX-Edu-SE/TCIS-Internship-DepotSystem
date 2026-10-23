using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;

/*
    Containers in the yard now, per line operator and dwell time.
    Dwell time is split at LongStayDays (default 10): short stay = fewer days, long stay = that many or more.
    The split point is a parameter because the plan leaves the exact day ranges to confirm.

    Feedback 7 + 8: the same filters as the detail rows (yard search, IYardVisitFilter), so a total always
    matches the rows listed under it. Main: line operator, type, size, dwell days. More: block, repair state,
    gate-in dates. The physical stock includes virtual blocks and leaves out containers at an outside repair
    facility (their visit is closed).
*/
public sealed record GetYardInventoryQuery(
    int LongStayDays = GetYardInventoryQuery.DefaultLongStayDays,
    Guid? OperatorId = null,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    int? MinDwellDays = null,
    int? MaxDwellDays = null,
    Guid? BlockId = null,
    YardRepairState? Repair = null,
    DateOnly? GateInFrom = null,
    DateOnly? GateInTo = null)
    : IQuery<Result<YardInventoryResponse>>, IYardVisitFilter
{
    public const int DefaultLongStayDays = 10;
}

/*
    AsOf / AsOfTime: the moment the dwell days were counted (feedback 7: hiển thị mốc tính);
    dwell days are Vietnam calendar days, 0 on the gate-in day.
*/
public sealed record YardInventoryResponse(
    DateOnly AsOf,
    DateTimeOffset AsOfTime,
    int LongStayDays,
    List<LineOperatorInventory> LineOperators,
    int TotalShortStay,
    int TotalLongStay,
    int Total);

// ShortStay: dwell days below LongStayDays; LongStay: LongStayDays or more
public sealed record LineOperatorInventory(
    Guid LineOperatorId,
    string Code,
    string Name,
    int ShortStay,
    int LongStay,
    int Total);
