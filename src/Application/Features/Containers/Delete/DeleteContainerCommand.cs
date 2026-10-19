using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Delete;

public sealed record DeleteContainerCommand(Guid Id) : ICommand;
