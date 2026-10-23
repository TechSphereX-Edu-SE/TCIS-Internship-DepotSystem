using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetForContainer;

// Every assignment of a container, cancelled ones included (the history of who operated it)
public sealed record GetContainerAssignmentsQuery(Guid ContainerId)
    : IQuery<Result<IReadOnlyList<OperatingAssignmentResponse>>>;
