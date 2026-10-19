using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

public sealed record GetAllContainersQuery(int Page = 1, int PageSize = 10)
    : IQuery<Result<PagedResult<ContainerResponse>>>;
