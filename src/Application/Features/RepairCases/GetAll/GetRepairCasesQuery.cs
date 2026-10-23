using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.GetAll;

/*
    Repair page list (feedback 8): main filters = container number, status, line operator;
    more = reported between two Vietnam dates (both included).
    Open: true = every status except Passed, false = Passed only, null = all.
    ContainerId: the cases of one container (e.g. from gate-out or the container catalog).
    More filters (feedback 8): External = sửa ngoài (true: sent or to be sent to a facility, false: repaired
    at the depot only), Facility = part of the facility name.
    Filters run in SQL before paging, so the count matches the rows.
*/
public sealed record GetRepairCasesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    RepairCaseStatus? Status = null,
    bool? Open = null,
    Guid? OperatorId = null,
    Guid? ContainerId = null,
    DateOnly? ReportedFrom = null,
    DateOnly? ReportedTo = null,
    bool? External = null,
    string? Facility = null)
    : IQuery<Result<PagedResult<RepairCaseSummary>>>;
