using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

// Search = part of the container number (e.g. "MSCU" or the full number), case-insensitive
public sealed record GetAllContainersQuery(int Page = 1, int PageSize = 10, string? Search = null)
    : IQuery<Result<PagedResult<ContainerResponse>>>;
