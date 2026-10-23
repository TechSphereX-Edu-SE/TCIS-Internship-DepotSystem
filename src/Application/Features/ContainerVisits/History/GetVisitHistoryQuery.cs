using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

/*
    Feedback 7: one visit opened from the history page - gate-in / gate-out, moves, inspection / repair
    cases and the documents that were stored with them. Only what was recorded is shown: data kept before
    a field existed (gate-in position, owner, moves before 23/10/2026) comes back null / empty, never rebuilt
    from today's values.
*/
public sealed record GetVisitHistoryQuery(Guid VisitId) : IQuery<Result<VisitHistoryDetail>>;

public sealed record VisitHistoryDetail(
    VisitHistoryRow Visit,
    VisitGateIn GateIn,
    VisitGateOut? GateOut,
    VisitPosition? CurrentPosition,
    List<VisitMove> Moves,
    List<VisitRepairCase> RepairCases,
    List<VisitDocument> Documents);

// Position is null when the gate-in position was not stored yet (older visits)
public sealed record VisitGateIn(
    DateTimeOffset At,
    string VehicleNo,
    ContainerCondition Condition,
    string? ConditionNote,
    ContainerGrade? LegacyGrade,
    VisitPosition? Position);

public sealed record VisitGateOut(
    DateTimeOffset At,
    string VehicleNo,
    GateOutPurpose? Purpose,
    string? DeliveryOrderNumber,
    string? CustomerName,
    string? RepairFacility);

// Bay/Row/Tier are null in a virtual block
public sealed record VisitPosition(Guid BlockId, string BlockCode, bool IsVirtual, int? Bay, int? Row, int? Tier);

public sealed record VisitMove(DateTimeOffset MovedAt, VisitPosition From, VisitPosition To, string? MovedBy);

// Cases opened during this visit; FirstFinding is the report that opened the case
public sealed record VisitRepairCase(
    Guid Id,
    RepairCaseStatus Status,
    bool? RepairNeeded,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? FirstFinding,
    int PhotoCount);

/*
    A stored document of the visit:
      OperatingAssignment  - căn cứ phân công used at gate-in (Reference = contract / instruction number)
      DeliveryOrder        - lệnh cấp rỗng the container left under
      ExternalRepairOut    - chỉ thị sửa ngoài the container left under
      ExternalRepairReturn - outside repair the container came back from at this gate-in
      RepairPhoto          - damage photo of a case of this visit (Reference = file name)
    LinkId: the order, the repair case or the assignment it opens.
*/
public sealed record VisitDocument(VisitDocumentKind Kind, string? Reference, DateTimeOffset? At, Guid? LinkId);

public enum VisitDocumentKind
{
    OperatingAssignment,
    DeliveryOrder,
    ExternalRepairOut,
    ExternalRepairReturn,
    RepairPhoto
}
