using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;

/*
    One row of the repair page list.
    OperatorCode: line operator of the visit the case was opened in (null when opened outside the yard).
    BlockCode/Bay/Row/Tier: where the container is now (null when it is not in the yard);
    the case itself never changes the position.
*/
public sealed record RepairCaseSummary(
    Guid Id,
    Guid ContainerId,
    string ContainerNumber,
    ContainerSize Size,
    ContainerCategory Category,
    RepairCaseStatus Status,
    string? OperatorCode,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string LatestFinding,
    int FindingCount,
    int PhotoCount,
    string? BlockCode,
    int? Bay,
    int? Row,
    int? Tier);

public sealed record RepairFindingResponse(
    Guid Id, string Description, RepairFindingSource Source, Guid? VisitId, DateTimeOffset ReportedAt, string? ReportedBy);

public sealed record RepairPhotoResponse(
    Guid Id, Guid? FindingId, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, string? UploadedBy);

public sealed record RepairCaseEventResponse(
    Guid Id, RepairEventKind Kind, RepairCaseStatus? FromStatus, RepairCaseStatus ToStatus, string? Note, DateTimeOffset At, string? By);

/*
    The whole case for the detail panel. AllowedActions comes from the same transition table the
    domain uses, so the screen only offers what the API accepts. ConditionAtGateIn is the condition
    recorded on the visit (kept even after the case is closed).
*/
public sealed record RepairCaseDetail(
    Guid Id,
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    string? OwnerCode,
    RepairCaseStatus Status,
    bool? RepairNeeded,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    Guid? VisitId,
    string? OperatorCode,
    ContainerCondition? ConditionAtGateIn,
    bool InYard,
    string? BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    IReadOnlyList<RepairAction> AllowedActions,
    IReadOnlyList<RepairFindingResponse> Findings,
    IReadOnlyList<RepairPhotoResponse> Photos,
    IReadOnlyList<RepairCaseEventResponse> History);
