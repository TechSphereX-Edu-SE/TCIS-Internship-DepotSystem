using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;

/*
    One operating period of a container (feedback 2). VisitCount = visits that took their line operator
    from it: such an assignment can still be ended, but no longer cancelled (that would rewrite history).
*/
public sealed record OperatingAssignmentResponse(
    Guid Id,
    Guid ContainerId,
    string ContainerNumber,
    Guid OperatorId,
    string OperatorCode,
    string OperatorName,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    OperatingAssignmentStatus Status,
    string? Reference,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    int VisitCount);
