using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

/*
    Feedback 5 + 8 (Vị trí & đảo chuyển): containers in the yard now, searchable and filtered, one page at a time.
    Only open visits are listed, in a physical or a virtual block; a container that is only in the catalog,
    has left, or is at an outside repair facility has no open visit, so it never shows up.
      - Search: part of the container number; exact match first, then prefix, then the rest.
      - Main filters: block, type, size. More: line operator of the visit, repair state, dwell days (from / to),
        gate-in dates (feedback 8, Tồn bãi).
    Without a search the rows follow Sort: the yard order (block, row, bay, tier) by default; the yard inventory
    detail uses Oldest (longest stay first) or Newest (feedback 7).
    The filters are shared with the yard inventory report (IYardVisitFilter), so its totals match these rows.
*/
public sealed record SearchYardContainersQuery(
    string? Search = null,
    Guid? BlockId = null,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    Guid? OperatorId = null,
    YardRepairState? Repair = null,
    int? MinDwellDays = null,
    int? MaxDwellDays = null,
    int Page = 1,
    int PageSize = 20,
    DateOnly? GateInFrom = null,
    DateOnly? GateInTo = null,
    YardSort Sort = YardSort.Position)
    : IQuery<Result<PagedResult<YardContainerResponse>>>, IYardVisitFilter;

// Row order when there is no search text
public enum YardSort
{
    Position,
    Oldest,
    Newest
}
