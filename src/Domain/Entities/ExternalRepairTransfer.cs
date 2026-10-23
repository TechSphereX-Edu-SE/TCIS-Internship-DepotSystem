using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Sending a container to an outside repair facility (feedback 4, sửa ngoài):
      - Requested on the repair page with the facility, the reason and the instruction / approval it rests on.
      - The gate-out page confirms the container really left (GatedOut): the visit is closed and the
        position freed, the container stays damaged, no delivery order is used, and the repair case stays open.
      - The next gate-in of the container is its return (Returned): a new visit linked here.
    The facility's "repaired" is not an acceptance: the case still needs its acceptance (nghiệm thu).
    At most one active transfer (Requested or GatedOut) per case.
*/
public sealed class ExternalRepairTransfer : AuditableEntity
{
    public Guid RepairCaseId { get; private set; }
    public RepairCase RepairCase { get; private set; } = default!;
    public Guid ContainerId { get; private set; }
    public Container Container { get; private set; } = default!;

    public string Facility { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    // Instruction / approval number the transfer rests on (chỉ thị / phê duyệt)
    public string Reference { get; private set; } = default!;

    public ExternalRepairStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }
    public string? RequestedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    // Visit closed by the gate-out to the facility, and the visit of the return
    public Guid? GateOutVisitId { get; private set; }
    public DateTimeOffset? GatedOutAt { get; private set; }
    public Guid? ReturnVisitId { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }

    // PostgreSQL xmin: a cancel and a gate-out at the same time cannot both succeed
    public uint RowVersion { get; private set; }

    public bool IsActive => Status is ExternalRepairStatus.Requested or ExternalRepairStatus.GatedOut;

    // A repair decision is needed first: the case must be approved for repair or already in repair
    public static readonly IReadOnlyList<RepairCaseStatus> AllowedCaseStatuses =
        [RepairCaseStatus.RepairApproved, RepairCaseStatus.InRepair];

    private ExternalRepairTransfer() { }

    /*
        The caller checks that the container is in the yard and that the case has no active transfer
        (both need the database). Returns the transfer and the history row of the case.
    */
    public static Result<(ExternalRepairTransfer Transfer, RepairCaseEvent Requested)> Request(
        RepairCase repairCase, string facility, string reason, string reference, DateTimeOffset at, string? actor)
    {
        if (!repairCase.IsOpen)
            return Result.Failure<(ExternalRepairTransfer, RepairCaseEvent)>(RepairCase.Closed());

        if (!AllowedCaseStatuses.Contains(repairCase.Status))
            return Result.Failure<(ExternalRepairTransfer, RepairCaseEvent)>(Error.Conflict("ExternalRepair.CaseNotApproved",
                $"An external repair needs a case approved for repair or in repair (now {repairCase.Status})."));

        var transfer = new ExternalRepairTransfer
        {
            RepairCaseId = repairCase.Id,
            ContainerId = repairCase.ContainerId,
            Facility = facility.Trim(),
            Reason = reason.Trim(),
            Reference = reference.Trim(),
            Status = ExternalRepairStatus.Requested,
            RequestedAt = at,
            RequestedBy = actor
        };
        // The reason stays on the transfer; the history line names where and on what instruction
        var requested = repairCase.Record(RepairEventKind.ExternalRequested,
            $"{transfer.Facility} ({transfer.Reference})", at, actor);
        return Result.Success((transfer, requested));
    }

    public Result<RepairCaseEvent> Cancel(RepairCase repairCase, string reason, DateTimeOffset at, string? actor)
    {
        if (Status != ExternalRepairStatus.Requested)
            return Result.Failure<RepairCaseEvent>(NotRequested());

        Status = ExternalRepairStatus.Cancelled;
        CancelledAt = at;
        CancelReason = reason.Trim();
        return Result.Success(repairCase.Record(RepairEventKind.ExternalCancelled, CancelReason, at, actor));
    }

    // Called by the gate-out to the facility, after the visit was closed
    public Result<RepairCaseEvent> MarkGatedOut(RepairCase repairCase, ContainerVisit visit, DateTimeOffset at, string? actor)
    {
        if (Status != ExternalRepairStatus.Requested)
            return Result.Failure<RepairCaseEvent>(NotRequested());

        Status = ExternalRepairStatus.GatedOut;
        GateOutVisitId = visit.Id;
        GatedOutAt = at;
        return Result.Success(repairCase.Record(RepairEventKind.SentForExternalRepair, Facility, at, actor));
    }

    // Called by the gate-in that brings the container back
    public Result<RepairCaseEvent> MarkReturned(RepairCase repairCase, ContainerVisit visit, DateTimeOffset at, string? actor)
    {
        if (Status != ExternalRepairStatus.GatedOut)
            return Result.Failure<RepairCaseEvent>(Error.Conflict("ExternalRepair.NotOut",
                "The container is not at the repair facility."));

        Status = ExternalRepairStatus.Returned;
        ReturnVisitId = visit.Id;
        ReturnedAt = at;
        return Result.Success(repairCase.Record(RepairEventKind.ReturnedFromExternalRepair, Facility, at, actor));
    }

    public static Error NotRequested() =>
        Error.Conflict("ExternalRepair.NotRequested", "The external repair transfer is no longer waiting to leave.");
}
