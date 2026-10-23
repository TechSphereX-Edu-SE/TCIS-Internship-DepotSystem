using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;

/*
    Containers in the yard now (open visits), optionally limited to one block and one row.
    Used by the yard map (bay x tier of a row) and by screens that list what is in the yard.
*/
public sealed record GetContainersInYardQuery(Guid? BlockId = null, int? Row = null)
    : IQuery<Result<IReadOnlyList<YardContainerResponse>>>;

// One container in the yard with its position; Bay/Row/Tier are null in a virtual block
public sealed record YardContainerResponse(
    Guid VisitId,
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    ContainerCondition Condition,
    string LineOperatorCode,
    Guid BlockId,
    string BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    DateTimeOffset GateInAt);
