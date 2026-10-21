using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    Current position of a container in the yard (spec section 3: "current position in the yard").
    Returned by the position query and after a move, so both show the same shape.
    Bay/Row/Tier are null when the container is in a virtual block.
*/
public sealed record ContainerPositionResponse(
    Guid VisitId,
    Guid ContainerId,
    string ContainerNumber,
    ContainerSize Size,
    string LineOperatorCode,
    Guid BlockId,
    string BlockCode,
    bool IsVirtualBlock,
    int? Bay,
    int? Row,
    int? Tier,
    DateTimeOffset GateInAt);
