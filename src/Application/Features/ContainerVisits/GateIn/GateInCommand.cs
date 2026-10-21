using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;

/*
    Gate-in: starts one container lifecycle at the depot (spec section 3).
    The fields are exactly what section 3 lists for a lifecycle at gate-in:
    line operator, position (Block/Bay/Row/Tier), grade (A, B, C...), condition,
    and the vehicle bringing the container in.
    Physical block: Bay/Row/Tier are required. Virtual block: all three are omitted.
    The gate-in time is taken from the server clock, not from the client.
*/
public sealed record GateInCommand(
    Guid ContainerId,
    Guid LineOperatorId,
    Guid BlockId,
    int? Bay,
    int? Row,
    int? Tier,
    ContainerGrade Grade,
    ContainerCondition Condition,
    string? ConditionNote,
    string VehicleNo)
    : ICommand<Result<GateInResponse>>, IYardPosition;

public sealed record GateInResponse(
    Guid VisitId,
    string ContainerNumber,
    string BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    DateTimeOffset GateInAt);
