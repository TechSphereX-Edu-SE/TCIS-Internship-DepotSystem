using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.GetAll;

public sealed class GetRepairCasesQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRepairCasesQuery, Result<PagedResult<RepairCaseSummary>>>
{
    public async Task<Result<PagedResult<RepairCaseSummary>>> HandleAsync(
        GetRepairCasesQuery query, CancellationToken cancellationToken = default)
    {
        var cases = dbContext.RepairCases.AsNoTracking().AsQueryable();

        if (query.ContainerId is { } containerId)
            cases = cases.Where(c => c.ContainerId == containerId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Container numbers are stored normalized (trim, upper case)
            var search = ContainerNumberRules.Normalize(query.Search);
            cases = cases.Where(c => c.Container.ContainerNumber.Contains(search));
        }

        if (query.Status is { } status)
            cases = cases.Where(c => c.Status == status);

        if (query.Open is { } open)
            cases = open
                ? cases.Where(c => c.Status != RepairCaseStatus.Passed)
                : cases.Where(c => c.Status == RepairCaseStatus.Passed);

        // Line operator of the visit the case was opened in (the operator of that lifecycle)
        if (query.OperatorId is { } operatorId)
            cases = cases.Where(c => c.Visit != null && c.Visit.LineOperatorId == operatorId);

        // Reported dates are Vietnam calendar days, both ends included
        if (query.ReportedFrom is { } from)
        {
            var start = DepotTime.StartOf(from);
            cases = cases.Where(c => c.OpenedAt >= start);
        }
        if (query.ReportedTo is { } to)
        {
            var end = DepotTime.StartOf(to.AddDays(1));
            cases = cases.Where(c => c.OpenedAt < end);
        }

        var totalCount = await cases.CountAsync(cancellationToken);

        // Newest report first; the id breaks ties so paging is stable
        var items = await cases
            .OrderByDescending(c => c.OpenedAt).ThenBy(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new
            {
                Case = c,
                c.Container.ContainerNumber,
                c.Container.Size,
                c.Container.Category,
                OperatorCode = c.Visit != null ? c.Visit.LineOperator.Code : null,
                LatestFinding = dbContext.RepairFindings
                    .Where(f => f.RepairCaseId == c.Id)
                    .OrderByDescending(f => f.ReportedAt)
                    .Select(f => f.Description)
                    .FirstOrDefault(),
                FindingCount = dbContext.RepairFindings.Count(f => f.RepairCaseId == c.Id),
                PhotoCount = dbContext.RepairPhotos.Count(p => p.RepairCaseId == c.Id),
                // Where the container is now: its open visit, if any
                Position = dbContext.ContainerVisits
                    .Where(v => v.ContainerId == c.ContainerId && v.GateOutAt == null)
                    .Select(v => new { v.Block.Code, v.Bay, v.Row, v.Tier })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var rows = items.Select(x => new RepairCaseSummary(
                x.Case.Id, x.Case.ContainerId, x.ContainerNumber, x.Size, x.Category, x.Case.Status,
                x.OperatorCode, x.Case.OpenedAt, x.Case.ClosedAt, x.LatestFinding ?? "", x.FindingCount, x.PhotoCount,
                x.Position?.Code, x.Position?.Bay, x.Position?.Row, x.Position?.Tier))
            .ToList();

        return Result.Success(new PagedResult<RepairCaseSummary>(rows, totalCount, query.Page, query.PageSize));
    }
}
