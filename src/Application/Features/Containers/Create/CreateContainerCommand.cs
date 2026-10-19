using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed record CreateContainerCommand(string ContainerNumber, string ContainerType)
    : ICommand<Result<CreateContainerResponse>>;

public sealed record CreateContainerResponse(Guid Id, string ContainerNumber, string ContainerType);
