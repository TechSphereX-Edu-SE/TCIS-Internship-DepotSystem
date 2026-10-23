using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

/*
    Counts gate-ins and gate-outs per Vietnam date and line operator.
    The events come from ThroughputFiltering, the same filters the detail rows use, so every total matches
    the rows listed under it. The matching times are grouped by Vietnam date in memory (DepotTime.DateOf).
    A gate-out belongs to the line operator of its visit.
*/
public sealed class GetThroughputQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetThroughputQuery, Result<ThroughputResponse>>
{
    private sealed record GateEvent(Guid LineOperatorId, DateTimeOffset At, bool IsGateIn, GateOutPurpose? Purpose);

    public async Task<Result<ThroughputResponse>> HandleAsync(
        GetThroughputQuery query, CancellationToken cancellationToken = default)
    {
        var operators = await dbContext.LineOperators
            .AsNoTracking()
            .Where(l => query.LineOperatorId == null || l.Id == query.LineOperatorId)
            .ToDictionaryAsync(l => l.Id, l => new { l.Code, l.Name }, cancellationToken);
        if (query.LineOperatorId is not null && operators.Count == 0)
            return Result.Failure<ThroughputResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{query.LineOperatorId}' was not found."));

        var gateIns = await dbContext.GateInsOf(query)
            .AsNoTracking()
            .Select(v => new GateEvent(v.LineOperatorId, v.GateInAt, true, null))
            .ToListAsync(cancellationToken);

        var gateOuts = await dbContext.GateOutsOf(query)
            .AsNoTracking()
            .Select(v => new GateEvent(v.LineOperatorId, v.GateOutAt!.Value, false, v.ExitPurpose))
            .ToListAsync(cancellationToken);

        var events = gateIns.Concat(gateOuts).ToList();
        string CodeOf(Guid id) => operators.TryGetValue(id, out var o) ? o.Code : string.Empty;

        var days = events
            .GroupBy(e => (Date: DepotTime.DateOf(e.At), e.LineOperatorId))
            .Select(g => new DailyThroughput(
                g.Key.Date,
                g.Key.LineOperatorId,
                CodeOf(g.Key.LineOperatorId),
                g.Count(e => e.IsGateIn),
                g.Count(e => !e.IsGateIn)))
            .OrderBy(d => d.Date)
            .ThenBy(d => d.LineOperatorCode)
            .ToList();

        // Per line operator, split by exit purpose: the rows each detail tab opens from
        var byOperator = events
            .GroupBy(e => e.LineOperatorId)
            .Select(g => new OperatorThroughput(
                g.Key,
                CodeOf(g.Key),
                operators.TryGetValue(g.Key, out var o) ? o.Name : string.Empty,
                g.Count(e => e.IsGateIn),
                g.Count(e => !e.IsGateIn),
                g.Count(e => e.Purpose == GateOutPurpose.EmptyRelease),
                g.Count(e => e.Purpose == GateOutPurpose.ExternalRepair)))
            .OrderBy(o => o.Code)
            .ToList();

        return Result.Success(new ThroughputResponse(
            query.From, query.To, days, gateIns.Count, gateOuts.Count,
            gateOuts.Count(e => e.Purpose == GateOutPurpose.EmptyRelease),
            gateOuts.Count(e => e.Purpose == GateOutPurpose.ExternalRepair),
            byOperator));
    }
}
