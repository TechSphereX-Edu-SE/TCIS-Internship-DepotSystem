using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateStatus;

public sealed record UpdateContainerStatusCommand(Guid Id, ContainerStatus Status) : ICommand;
