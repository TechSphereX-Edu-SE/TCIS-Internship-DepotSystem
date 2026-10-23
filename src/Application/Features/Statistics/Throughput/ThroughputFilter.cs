using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

/*
    Filters of the throughput report (Sản lượng), shared by the totals and the detail rows so they
    always count the same events (feedback 7 + 8: tổng khớp chi tiết).
      - From / To: Vietnam dates, both included (01-10 = ten days).
      - Main: line operator of the visit, type, size. More: exit purpose.
    Purpose only narrows the gate-outs: a gate-in has no exit purpose yet (inference: a gate-in is not
    hidden because its container later left for repair).
*/
public interface IThroughputFilter
{
    DateOnly From { get; }
    DateOnly To { get; }
    Guid? LineOperatorId { get; }
    ContainerCategory? Category { get; }
    ContainerSize? Size { get; }
    GateOutPurpose? Purpose { get; }
}

public static class ThroughputFiltering
{
    // Longest range in one request, to keep the result readable and the query bounded
    public const int MaxDays = 366;

    /*
        Gate-in events of the period: filtered by gate-in time only. A container that came in during the
        period and left later is still a gate-in of this period.
    */
    public static IQueryable<ContainerVisit> GateInsOf(this IAppDbContext dbContext, IThroughputFilter filter)
    {
        var (start, end) = Range(filter);
        return Visits(dbContext, filter).Where(v => v.GateInAt >= start && v.GateInAt < end);
    }

    /*
        Gate-out events of the period: filtered by gate-out time only, so a container that came in before
        the period but left during it counts here (feedback 7). A visit is one event of each kind: a
        container that came back several times counts once per visit, never merged.
    */
    public static IQueryable<ContainerVisit> GateOutsOf(this IAppDbContext dbContext, IThroughputFilter filter)
    {
        var (start, end) = Range(filter);
        var visits = Visits(dbContext, filter).Where(v => v.GateOutAt >= start && v.GateOutAt < end);
        return filter.Purpose is { } purpose ? visits.Where(v => v.ExitPurpose == purpose) : visits;
    }

    // Vietnam dates -> [From 00:00 +07, To+1 00:00 +07), a plain comparison the indexes can use
    private static (DateTimeOffset Start, DateTimeOffset End) Range(IThroughputFilter filter) =>
        (DepotTime.StartOf(filter.From), DepotTime.StartOf(filter.To.AddDays(1)));

    private static IQueryable<ContainerVisit> Visits(IAppDbContext dbContext, IThroughputFilter filter)
    {
        var visits = dbContext.ContainerVisits.AsQueryable();
        // The operator of the visit, not the container's operator today
        if (filter.LineOperatorId is { } operatorId)
            visits = visits.Where(v => v.LineOperatorId == operatorId);
        if (filter.Category is { } category)
            visits = visits.Where(v => v.Container.Category == category);
        if (filter.Size is { } size)
            visits = visits.Where(v => v.Container.Size == size);
        return visits;
    }
}

// Date range and filter shapes, used by the totals and the detail validators (via Include)
public sealed class ThroughputFilterValidator : AbstractValidator<IThroughputFilter>
{
    public ThroughputFilterValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("'To' must be on or after 'From'.");

        // Both dates are included, so the range has (To - From + 1) days
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < ThroughputFiltering.MaxDays)
            .When(x => x.To >= x.From)
            .WithName("Range")
            .WithMessage($"The range can be at most {ThroughputFiltering.MaxDays} days.");

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
    }
}
