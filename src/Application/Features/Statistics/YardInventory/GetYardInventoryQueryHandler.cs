using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;

/*
    Counts the open visits (not gated out) per line operator and dwell-time group.
    Dwell days use Vietnam calendar days (DepotTime.DwellDays), which SQL cannot compute
    the same way, so only the gate-in times are read and grouped in memory. The yard holds
    a bounded number of containers, so this stays small.
    Every line operator is listed, with zeros when it has no container in the yard.
*/
public sealed class GetYardInventoryQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetYardInventoryQuery, Result<YardInventoryResponse>>
{
    public async Task<Result<YardInventoryResponse>> HandleAsync(
        GetYardInventoryQuery query, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var lineOperators = await dbContext.LineOperators
            .AsNoTracking()
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code, l.Name })
            .ToListAsync(cancellationToken);

        var inYard = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.GateOutAt == null)
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

        var rows = lineOperators
            .Select(l =>
            {
                var (shortStay, longStay) = byOperator.GetValueOrDefault(l.Id);
                return new LineOperatorInventory(l.Id, l.Code, l.Name, shortStay, longStay, shortStay + longStay);
            })
            .ToList();

        var totalShort = rows.Sum(r => r.ShortStay);
        var totalLong = rows.Sum(r => r.LongStay);
        return Result.Success(new YardInventoryResponse(
            DepotTime.DateOf(now), query.LongStayDays, rows, totalShort, totalLong, totalShort + totalLong));
    }
}
