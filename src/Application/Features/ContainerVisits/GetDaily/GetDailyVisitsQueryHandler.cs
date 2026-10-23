using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetDaily;

/*
    A Vietnam date is turned into the time range [date 00:00 +07, next day 00:00 +07),
    the same way as the throughput statistics, so events between 00:00 and 07:00 VN
    are not counted on the previous (UTC) day.
    Both lists are newest first (the event the user just recorded is on top).
*/
public sealed class GetDailyVisitsQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetDailyVisitsQuery, Result<DailyVisitsResponse>>
{
    public async Task<Result<DailyVisitsResponse>> HandleAsync(
        GetDailyVisitsQuery query, CancellationToken cancellationToken = default)
    {
        var date = query.Date ?? DepotTime.DateOf(timeProvider.GetUtcNow());
        var start = DepotTime.StartOf(date);
        var end = DepotTime.StartOf(date.AddDays(1));

        var gateIns = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.GateInAt >= start && v.GateInAt < end)
            // Feedback 1: newest gate-in first; the container number breaks ties at the same time
            .OrderByDescending(v => v.GateInAt)
            .ThenBy(v => v.Container.ContainerNumber)
            .Select(v => new DailyGateInResponse(
                v.Id, v.Container.ContainerNumber, v.Container.IsoCode, v.Container.Size, v.Container.Category,
                v.LineOperator.Code, v.Block.Code, v.Bay, v.Row, v.Tier, v.Grade,
                v.GateInVehicleNo, v.GateInAt))
            .ToListAsync(cancellationToken);

        var gateOuts = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.GateOutAt >= start && v.GateOutAt < end)
            // Newest gate-out first, same tie-break as the gate-ins
            .OrderByDescending(v => v.GateOutAt)
            .ThenBy(v => v.Container.ContainerNumber)
            .Select(v => new DailyGateOutResponse(
                v.Id, v.Container.ContainerNumber, v.Container.IsoCode, v.Container.Size, v.Container.Category,
                v.LineOperator.Code,
                v.DeliveryOrder != null ? v.DeliveryOrder.OrderNumber : null,
                // The order line of the same container type (one line per type, enforced at creation)
                v.DeliveryOrder != null
                    ? v.DeliveryOrder.Lines.Where(l => l.Category == v.Container.Category).Select(l => (int?)l.Quantity).FirstOrDefault()
                    : null,
                v.DeliveryOrder != null
                    ? v.DeliveryOrder.Lines.Where(l => l.Category == v.Container.Category).Select(l => (int?)(l.Quantity - l.ReleasedQuantity)).FirstOrDefault()
                    : null,
                v.GateOutVehicleNo, v.GateOutAt!.Value))
            .ToListAsync(cancellationToken);

        return Result.Success(new DailyVisitsResponse(date, gateIns, gateOuts));
    }
}
