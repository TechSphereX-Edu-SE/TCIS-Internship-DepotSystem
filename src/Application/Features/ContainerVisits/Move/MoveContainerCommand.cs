using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;

/*
    Moves a container in the yard to another position (re-stacking), in the same block
    or another one, physical or virtual (e.g. a damaged container to the repair area).
    Physical block: Bay/Row/Tier are required. Virtual block: all three are omitted.
    FromBlockId..FromTier (optional, feedback 5): where the screen saw the container. When given and the
    container has moved since, the move is refused (409 ContainerVisit.PositionChanged) instead of
    moving it from a place the clerk did not check.
*/
public sealed record MoveContainerCommand(
    Guid ContainerId,
    Guid BlockId,
    int? Bay,
    int? Row,
    int? Tier,
    Guid? FromBlockId = null,
    int? FromBay = null,
    int? FromRow = null,
    int? FromTier = null)
    : ICommand<Result<ContainerPositionResponse>>, IYardPosition;
