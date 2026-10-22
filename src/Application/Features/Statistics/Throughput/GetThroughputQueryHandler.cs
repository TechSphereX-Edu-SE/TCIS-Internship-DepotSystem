using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

/*
    Counts gate-ins and gate-outs per Vietnam date and line operator.
    The Vietnam date range is turned into a time range [From 00:00 +07, To+1 00:00 +07) so the
    database filters with the (LineOperatorId, GateInAt/GateOutAt) indexes; the matching times
    are then grouped by Vietnam date in memory (DepotTime.DateOf).
    A gate-out belongs to the line operator of its visit.
*/
public sealed class GetThroughputQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetThroughputQuery, Result<ThroughputResponse>>
{
    private sealed record GateEvent(Guid LineOperatorId, DateTimeOffset At, bool IsGateIn);

    public async Task<Result<ThroughputResponse>> HandleAsync(
        GetThroughputQuery query, CancellationToken cancellationToken = default)
    {
        var operatorCodes = await dbContext.LineOperators
            .AsNoTracking()
            .Where(l => query.LineOperatorId == null || l.Id == query.LineOperatorId)
            .ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        if (query.LineOperatorId is not null && operatorCodes.Count == 0)
            return Result.Failure<ThroughputResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{query.LineOperatorId}' was not found."));

        var start = DepotTime.StartOf(query.From);
        var end = DepotTime.StartOf(query.To.AddDays(1));

        var visits = dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => query.LineOperatorId == null || v.LineOperatorId == query.LineOperatorId);

        var gateIns = await visits
            .Where(v => v.GateInAt >= start && v.GateInAt < end)
            .Select(v => new GateEvent(v.LineOperatorId, v.GateInAt, true))
            .ToListAsync(cancellationToken);

        var gateOuts = await visits
            .Where(v => v.GateOutAt >= start && v.GateOutAt < end)
            .Select(v => new GateEvent(v.LineOperatorId, v.GateOutAt!.Value, false))
            .ToListAsync(cancellationToken);

        var days = gateIns
            .Concat(gateOuts)
            .GroupBy(e => (Date: DepotTime.DateOf(e.At), e.LineOperatorId))
            .Select(g => new DailyThroughput(
                g.Key.Date,
                g.Key.LineOperatorId,
                operatorCodes.GetValueOrDefault(g.Key.LineOperatorId, string.Empty),
                g.Count(e => e.IsGateIn),
                g.Count(e => !e.IsGateIn)))
            .OrderBy(d => d.Date)
            .ThenBy(d => d.LineOperatorCode)
            .ToList();

        return Result.Success(new ThroughputResponse(
            query.From, query.To, days, gateIns.Count, gateOuts.Count));
    }
}
