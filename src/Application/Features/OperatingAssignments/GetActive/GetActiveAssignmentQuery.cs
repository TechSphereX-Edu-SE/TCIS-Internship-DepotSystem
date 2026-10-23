using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetActive;

// The confirmed assignment of a container on a Vietnam date (default: today) - what gate-in will use
public sealed record GetActiveAssignmentQuery(Guid ContainerId, DateOnly? Date = null)
    : IQuery<Result<OperatingAssignmentResponse>>;
