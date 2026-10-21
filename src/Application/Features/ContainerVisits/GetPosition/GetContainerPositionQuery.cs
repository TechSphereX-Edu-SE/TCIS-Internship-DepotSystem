using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;

// Looks up where a container is now, by its container number (what yard staff read on the box)
public sealed record GetContainerPositionQuery(string ContainerNumber) : IQuery<Result<ContainerPositionResponse>>;
