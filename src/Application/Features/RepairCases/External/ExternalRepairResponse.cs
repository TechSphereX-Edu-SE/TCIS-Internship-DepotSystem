using System.Linq.Expressions;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

/*
    One transfer to an outside repair facility (feedback 4, sửa ngoài), for the repair case detail and
    for the gate-out page (transfers waiting to leave). BlockCode..Tier = where the container is now
    (null when it is not in the yard, e.g. at the facility).
*/
public sealed record ExternalRepairResponse(
    Guid Id,
    Guid RepairCaseId,
    RepairCaseStatus CaseStatus,
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    ExternalRepairStatus Status,
    string Facility,
    string Reason,
    string Reference,
    DateTimeOffset RequestedAt,
    string? RequestedBy,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    DateTimeOffset? GatedOutAt,
    DateTimeOffset? ReturnedAt,
    string? BlockCode,
    int? Bay,
    int? Row,
    int? Tier);

public static class ExternalRepairProjection
{
    // The current position comes from the open visit of the container, if any
    public static Expression<Func<ExternalRepairTransfer, ExternalRepairResponse>> ToResponse(
        IQueryable<ContainerVisit> visits) =>
        t => new ExternalRepairResponse(
            t.Id, t.RepairCaseId, t.RepairCase.Status, t.ContainerId,
            t.Container.ContainerNumber, t.Container.IsoCode, t.Container.Size, t.Container.Category,
            t.Status, t.Facility, t.Reason, t.Reference, t.RequestedAt, t.RequestedBy,
            t.CancelledAt, t.CancelReason, t.GatedOutAt, t.ReturnedAt,
            visits.Where(v => v.ContainerId == t.ContainerId && v.GateOutAt == null).Select(v => v.Block.Code).FirstOrDefault(),
            visits.Where(v => v.ContainerId == t.ContainerId && v.GateOutAt == null).Select(v => v.Bay).FirstOrDefault(),
            visits.Where(v => v.ContainerId == t.ContainerId && v.GateOutAt == null).Select(v => v.Row).FirstOrDefault(),
            visits.Where(v => v.ContainerId == t.ContainerId && v.GateOutAt == null).Select(v => v.Tier).FirstOrDefault());
}
