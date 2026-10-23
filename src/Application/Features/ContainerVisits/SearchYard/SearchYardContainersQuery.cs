using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

/*
    Feedback 5 + 8 (Vị trí & đảo chuyển): containers in the yard now, searchable and filtered, one page at a time.
    Only open visits are listed, in a physical or a virtual block; a container that is only in the catalog,
    has left, or is at an outside repair facility has no open visit, so it never shows up.
      - Search: part of the container number; exact match first, then prefix, then the rest.
      - Main filters: block, type, size. More: line operator of the visit, repair state, dwell days (from / to).
    Without a search the rows follow the yard: block, row, bay, tier.
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
    int PageSize = 20)
    : IQuery<Result<PagedResult<YardContainerResponse>>>;

/*
    Repair state of a container in the yard (feedback 5 keeps the two marks apart):
      None       - no open repair case
      Abnormal   - có bất thường, chờ giám định (case AwaitingInspection)
      InRepair   - đã xác định cần sửa / đang xử lý (every other open status)
      AnyOpen    - Abnormal or InRepair
*/
public enum YardRepairState
{
    None,
    Abnormal,
    InRepair,
    AnyOpen
}
