using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;

/*
    Moves a container in the yard to another position (re-stacking), in the same block
    or another one, physical or virtual (e.g. a damaged container to the repair area).
    Physical block: Bay/Row/Tier are required. Virtual block: all three are omitted.
*/
public sealed record MoveContainerCommand(
    Guid ContainerId,
    Guid BlockId,
    int? Bay,
    int? Row,
    int? Tier)
    : ICommand<Result<ContainerPositionResponse>>, IYardPosition;
