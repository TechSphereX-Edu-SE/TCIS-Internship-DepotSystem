using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

/*
    Search = part of the container number (e.g. "MSCU" or the full number), case-insensitive;
    results are ranked exact match, then prefix, then the rest.
    OperatorId = only containers whose confirmed operating assignment covers today with that operator
    (feedback 2/3: choosing CMA finds the HMM-owned container CMA rents, never by number prefix).
*/
public sealed record GetAllContainersQuery(int Page = 1, int PageSize = 10, string? Search = null, Guid? OperatorId = null)
    : IQuery<Result<PagedResult<ContainerResponse>>>;
