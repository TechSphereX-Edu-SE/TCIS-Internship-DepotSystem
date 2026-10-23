using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetGateOutCandidates;

/*
    Order of the rows: best number match first (when searching), then the containers that can leave,
    then the blocked ones, then by number. Repair blocks are known in SQL, so they are sorted before paging;
    "container above" needs the yard rules and is worked out for the rows of the page only.
    A cancelled or expired order gives 409 (the dropdown of orders only offers usable ones).
*/
public sealed class GetGateOutCandidatesQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetGateOutCandidatesQuery, Result<PagedResult<GateOutCandidate>>>
{
    public async Task<Result<PagedResult<GateOutCandidate>>> HandleAsync(
        GetGateOutCandidatesQuery query, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.DeliveryOrders.AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == query.DeliveryOrderId, cancellationToken);
        if (order is null)
            return Result.Failure<PagedResult<GateOutCandidate>>(Error.NotFound("DeliveryOrder.NotFound",
                $"Delivery order with ID '{query.DeliveryOrderId}' was not found."));

        if (order.IsCancelled)
            return Result.Failure<PagedResult<GateOutCandidate>>(Error.Conflict("DeliveryOrder.Cancelled",
                $"Delivery order {order.OrderNumber} has been cancelled."));
        if (order.IsExpired(DepotTime.DateOf(timeProvider.GetUtcNow())))
            return Result.Failure<PagedResult<GateOutCandidate>>(Error.Conflict("DeliveryOrder.Expired",
                $"Delivery order {order.OrderNumber} expired on {order.ExpiryDate:dd/MM/yyyy}."));

        // Types still allowed for each size (a line without size takes both sizes)
        var open = order.Lines.Where(l => l.RemainingQuantity > 0).ToList();
        var types20 = open.Where(l => l.Size != ContainerSize.Ft40).Select(l => l.Category).Distinct().ToList();
        var types40 = open.Where(l => l.Size != ContainerSize.Ft20).Select(l => l.Category).Distinct().ToList();

        var visits = dbContext.ContainerVisits.AsNoTracking()
            .Where(v => v.GateOutAt == null && v.LineOperatorId == order.LineOperatorId)
            .Where(v => (v.Container.Size == ContainerSize.Ft20 && types20.Contains(v.Container.Category)) ||
                        (v.Container.Size == ContainerSize.Ft40 && types40.Contains(v.Container.Category)));

        // Numbers are stored normalized (trim, upper case); "" = no search
        var search = string.IsNullOrWhiteSpace(query.Search) ? "" : ContainerNumberRules.Normalize(query.Search);
        if (search.Length > 0)
            visits = visits.Where(v => v.Container.ContainerNumber.Contains(search));

        var rows = visits.Select(v => new
        {
            Visit = v,
            v.Container,
            BlockCode = v.Block.Code,
            // At most one open case per container (unique index), so FirstOrDefault is that case
            OpenCaseId = dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefault(),
            OpenCaseStatus = dbContext.RepairCases
                .Where(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.Passed)
                .Select(c => (RepairCaseStatus?)c.Status)
                .FirstOrDefault()
        });

        var totalCount = await rows.CountAsync(cancellationToken);

        var ordered = search.Length == 0
            ? rows.OrderBy(r => r.OpenCaseId != null || r.Container.Condition == ContainerCondition.Damaged ? 1 : 0)
                .ThenBy(r => r.Container.ContainerNumber)
            : rows.OrderBy(r => r.Container.ContainerNumber == search ? 0 : r.Container.ContainerNumber.StartsWith(search) ? 1 : 2)
                .ThenBy(r => r.OpenCaseId != null || r.Container.Condition == ContainerCondition.Damaged ? 1 : 0)
                .ThenBy(r => r.Container.ContainerNumber);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<GateOutCandidate>(page.Count);
        foreach (var r in page)
        {
            var v = r.Visit;
            var above = r.OpenCaseId is null
                ? await FindContainerAboveAsync(v.BlockId, v.Bay, v.Row, v.Tier, r.Container.Size, cancellationToken)
                : null;
            var reason = r.OpenCaseId is not null ? "RepairCase"
                : r.Container.Condition == ContainerCondition.Damaged ? "Damaged"
                : above is not null ? "ContainerAbove"
                : null;

            items.Add(new GateOutCandidate(
                v.Id, r.Container.Id, r.Container.ContainerNumber, r.Container.IsoCode, r.Container.Size, r.Container.Category,
                r.BlockCode, v.Bay, v.Row, v.Tier, r.Container.Condition, v.GateInAt,
                reason, r.OpenCaseId, r.OpenCaseStatus, above));
        }

        return Result.Success(new PagedResult<GateOutCandidate>(items, totalCount, query.Page, query.PageSize));
    }

    // Number of the container resting on this one (same rule as YardPlacement.CheckNothingAboveAsync), or null
    private async Task<string?> FindContainerAboveAsync(
        Guid blockId, int? bay, int? row, int? tier, ContainerSize size, CancellationToken cancellationToken)
    {
        if (bay is null || row is null || tier is null)
            return null; // virtual block: no stacking

        var upper = await dbContext.ContainerVisits.AsNoTracking()
            .Where(v => v.BlockId == blockId && v.GateOutAt == null && v.Row == row && v.Tier == tier + 1)
            .Select(v => new { Bay = v.Bay!.Value, Tier = v.Tier!.Value, v.Container.Size, v.Container.ContainerNumber })
            .ToListAsync(cancellationToken);

        return upper.FirstOrDefault(u =>
            YardSlotRules.Overlaps(bay.Value, tier.Value + 1, size, [new YardSlotRules.PlacedContainer(u.Bay, u.Tier, u.Size)]))
            ?.ContainerNumber;
    }
}
