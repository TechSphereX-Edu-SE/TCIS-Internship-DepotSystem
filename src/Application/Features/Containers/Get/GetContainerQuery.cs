using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Get;

public sealed record GetContainerQuery(Guid Id) : IQuery<Result<ContainerResponse>>;

public sealed record ContainerResponse(
    Guid Id,
    string ContainerNumber,
    string ContainerType,
    string Status,
    DateTimeOffset? DepartedAt,
    DateTimeOffset CreatedAt)
{
    public static ContainerResponse From(Container c) => new(
        c.Id, c.ContainerNumber, c.ContainerType,
        c.Status.ToString(), c.DepartedAt, c.CreatedAt);
}
