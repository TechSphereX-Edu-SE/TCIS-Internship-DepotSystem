using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    Filters on containers in the yard, shared by the yard search (Vị trí & đảo chuyển) and the yard
    inventory report (Tồn bãi). Both apply them with ApplyYardFilter, so a total and its detail rows
    always come from the same conditions (feedback 8: lọc tổng khớp chi tiết).
*/
public interface IYardVisitFilter
{
    Guid? BlockId { get; }
    ContainerCategory? Category { get; }
    ContainerSize? Size { get; }

    // Line operator of the visit (who the container came in for), not today's assignment
    Guid? OperatorId { get; }
    YardRepairState? Repair { get; }
    int? MinDwellDays { get; }
    int? MaxDwellDays { get; }

    // Vietnam dates, both included
    DateOnly? GateInFrom { get; }
    DateOnly? GateInTo { get; }
}

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

public static class YardVisitFiltering
{
    /*
        Open visits (in a physical or virtual block) matching every given filter, as a query for SQL.
        A container at an outside repair facility has left (its visit is closed), so it is never counted:
        the report shows the physical stock of the depot.
    */
    public static IQueryable<ContainerVisit> InYard(this IAppDbContext dbContext, IYardVisitFilter filter, DateOnly today)
    {
        var visits = dbContext.ContainerVisits.Where(v => v.GateOutAt == null);

        if (filter.BlockId is { } blockId)
            visits = visits.Where(v => v.BlockId == blockId);
        if (filter.Category is { } category)
            visits = visits.Where(v => v.Container.Category == category);
        if (filter.Size is { } size)
            visits = visits.Where(v => v.Container.Size == size);
        if (filter.OperatorId is { } operatorId)
            visits = visits.Where(v => v.LineOperatorId == operatorId);
        if (filter.Repair is { } repair)
            visits = WithRepairState(dbContext, visits, repair);

        // Gate-in period: [From 00:00 +07, To+1 00:00 +07), so a plain comparison on GateInAt
        if (filter.GateInFrom is { } gateInFrom)
        {
            var start = DepotTime.StartOf(gateInFrom);
            visits = visits.Where(v => v.GateInAt >= start);
        }
        if (filter.GateInTo is { } gateInTo)
        {
            var end = DepotTime.StartOf(gateInTo.AddDays(1));
            visits = visits.Where(v => v.GateInAt < end);
        }

        return WithDwellDays(visits, filter.MinDwellDays, filter.MaxDwellDays, today);
    }

    // Open case of the container, by its status (see YardRepairState)
    private static IQueryable<ContainerVisit> WithRepairState(
        IAppDbContext dbContext, IQueryable<ContainerVisit> visits, YardRepairState state)
    {
        var open = dbContext.RepairCases.Where(c => c.Status != RepairCaseStatus.Passed);
        return state switch
        {
            YardRepairState.None => visits.Where(v => !open.Any(c => c.ContainerId == v.ContainerId)),
            YardRepairState.Abnormal => visits.Where(v =>
                open.Any(c => c.ContainerId == v.ContainerId && c.Status == RepairCaseStatus.AwaitingInspection)),
            YardRepairState.InRepair => visits.Where(v =>
                open.Any(c => c.ContainerId == v.ContainerId && c.Status != RepairCaseStatus.AwaitingInspection)),
            _ => visits.Where(v => open.Any(c => c.ContainerId == v.ContainerId))
        };
    }

    /*
        Dwell days are Vietnam calendar days (DepotTime.DwellDays): 0 on the gate-in day.
        At least N days = came in on or before today - N; at most N days = on or after today - N.
        Turned into instants so the condition stays a plain comparison on GateInAt.
    */
    private static IQueryable<ContainerVisit> WithDwellDays(
        IQueryable<ContainerVisit> visits, int? minDays, int? maxDays, DateOnly today)
    {
        if (minDays is { } min)
        {
            var before = DepotTime.StartOf(today.AddDays(-min + 1));
            visits = visits.Where(v => v.GateInAt < before);
        }
        if (maxDays is { } max)
        {
            var from = DepotTime.StartOf(today.AddDays(-max));
            visits = visits.Where(v => v.GateInAt >= from);
        }
        return visits;
    }
}

// Shape checks of the shared filters, used by both query validators (via Include)
public sealed class YardVisitFilterValidator : AbstractValidator<IYardVisitFilter>
{
    public YardVisitFilterValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Repair).IsInEnum();
        RuleFor(x => x.MinDwellDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxDwellDays).GreaterThanOrEqualTo(0);

        // "From 10 to 5 days" would always be empty: report it instead of answering nothing
        RuleFor(x => x.MaxDwellDays)
            .GreaterThanOrEqualTo(x => x.MinDwellDays!.Value)
            .When(x => x.MinDwellDays is not null && x.MaxDwellDays is not null);
        RuleFor(x => x.GateInTo)
            .GreaterThanOrEqualTo(x => x.GateInFrom!.Value)
            .When(x => x.GateInFrom is not null && x.GateInTo is not null)
            .WithMessage("'Gate-in to' must be on or after 'Gate-in from'.");
    }
}
