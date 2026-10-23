using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

/*
    One visit (lượt) of a container, as the history page and the throughput detail list it (feedback 7).
      - VisitRef: short code of the visit (first 8 characters of its id). Visits have no number of their own,
        so this only tells two visits of the same container apart.
      - OwnerCode / OwnerName: owner recorded at gate-in; null = no historical data (older visits, or the
        container had no owner yet). Never filled from today's owner.
      - LineOperatorCode: operator of the visit, not the container's operator today.
      - DwellDays: Vietnam calendar days, 0 on the gate-in day; to the gate-out for a closed visit,
        to today for an open one.
      - DeliveryOrderNumber / RepairFacility: what the container left under (cấp rỗng / sửa ngoài).
*/
public sealed record VisitHistoryRow(
    Guid VisitId,
    string VisitRef,
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerCategory Category,
    ContainerSize Size,
    string? OwnerCode,
    string? OwnerName,
    Guid LineOperatorId,
    string LineOperatorCode,
    DateTimeOffset GateInAt,
    DateTimeOffset? GateOutAt,
    bool IsOpen,
    int DwellDays,
    GateOutPurpose? ExitPurpose,
    Guid? DeliveryOrderId,
    string? DeliveryOrderNumber,
    string? RepairFacility);

public static class VisitHistoryRows
{
    // Columns read in SQL; dwell days and the short code are worked out in memory (Vietnam calendar days)
    private sealed record RowData(
        Guid VisitId, Guid ContainerId, string ContainerNumber, string IsoCode,
        ContainerCategory Category, ContainerSize Size, string? OwnerCode, string? OwnerName,
        Guid LineOperatorId, string LineOperatorCode, DateTimeOffset GateInAt, DateTimeOffset? GateOutAt,
        GateOutPurpose? ExitPurpose, Guid? DeliveryOrderId, string? DeliveryOrderNumber, string? RepairFacility);

    // Reads one page of already filtered and ordered visits as history rows
    public static async Task<List<VisitHistoryRow>> ToHistoryRowsAsync(
        this IQueryable<ContainerVisit> visits, IAppDbContext dbContext, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var data = await visits
            .Select(v => new RowData(
                v.Id, v.ContainerId, v.Container.ContainerNumber, v.Container.IsoCode,
                v.Container.Category, v.Container.Size,
                v.Owner != null ? v.Owner.Code : null,
                v.Owner != null ? v.Owner.Name : null,
                v.LineOperatorId, v.LineOperator.Code, v.GateInAt, v.GateOutAt,
                v.ExitPurpose, v.DeliveryOrderId,
                v.DeliveryOrder != null ? v.DeliveryOrder.OrderNumber : null,
                // The outside repair transfer this visit left under (at most one per gate-out)
                dbContext.ExternalRepairTransfers
                    .Where(t => t.GateOutVisitId == v.Id)
                    .Select(t => t.Facility)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return data.Select(d => ToRow(d, now)).ToList();
    }

    private static VisitHistoryRow ToRow(RowData d, DateTimeOffset now) => new(
        d.VisitId, ShortRef(d.VisitId), d.ContainerId, d.ContainerNumber, d.IsoCode, d.Category, d.Size,
        d.OwnerCode, d.OwnerName, d.LineOperatorId, d.LineOperatorCode, d.GateInAt, d.GateOutAt,
        d.GateOutAt is null, DepotTime.DwellDays(d.GateInAt, d.GateOutAt ?? now),
        d.ExitPurpose, d.DeliveryOrderId, d.DeliveryOrderNumber, d.RepairFacility);

    public static string ShortRef(Guid visitId) => visitId.ToString("N")[..8].ToUpperInvariant();
}
