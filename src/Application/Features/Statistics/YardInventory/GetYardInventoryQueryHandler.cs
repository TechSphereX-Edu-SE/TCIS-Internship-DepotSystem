using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;

/*
    Counts the open visits (not gated out) per line operator and dwell-time group.
    The visits come from the shared yard filter (YardVisitFiltering.InYard), the same one the detail rows
    use, so the counts and the rows always agree.
    Dwell days use Vietnam calendar days (DepotTime.DwellDays), which SQL cannot compute the same way,
    so only the gate-in times are read and grouped in memory. The yard holds a bounded number of containers,
    so this stays small.
    Every line operator is listed (zeros when it has nothing in the yard), or only the one asked for.
*/
public sealed class GetYardInventoryQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetYardInventoryQuery, Result<YardInventoryResponse>>
{
    public async Task<Result<YardInventoryResponse>> HandleAsync(
        GetYardInventoryQuery query, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var today = DepotTime.DateOf(now);

        var lineOperators = await dbContext.LineOperators
            .AsNoTracking()
            .Where(l => query.OperatorId == null || l.Id == query.OperatorId)
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code, l.Name, l.IsOperator })
            .ToListAsync(cancellationToken);
        if (query.OperatorId is not null && lineOperators.Count == 0)
            return Result.Failure<YardInventoryResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{query.OperatorId}' was not found."));

        var inYard = await dbContext.InYard(query, today)
            .AsNoTracking()
            .Select(v => new { v.LineOperatorId, v.GateInAt })
            .ToListAsync(cancellationToken);

        // Short/long stay counts per line operator
        var byOperator = inYard
            .GroupBy(v => v.LineOperatorId)
            .ToDictionary(g => g.Key, g =>
            {
                var longStay = g.Count(v => DepotTime.DwellDays(v.GateInAt, now) >= query.LongStayDays);
                return (ShortStay: g.Count() - longStay, LongStay: longStay);
            });

        // An owner-only partner (leasing company) never operates containers: listed only if it has some
        var rows = lineOperators
            .Where(l => l.IsOperator || byOperator.ContainsKey(l.Id))
            .Select(l =>
            {
                var (shortStay, longStay) = byOperator.GetValueOrDefault(l.Id);
                return new LineOperatorInventory(l.Id, l.Code, l.Name, shortStay, longStay, shortStay + longStay);
            })
            .ToList();

        var totalShort = rows.Sum(r => r.ShortStay);
        var totalLong = rows.Sum(r => r.LongStay);
        return Result.Success(new YardInventoryResponse(
            today, now, query.LongStayDays, rows, totalShort, totalLong, totalShort + totalLong));
    }
}
