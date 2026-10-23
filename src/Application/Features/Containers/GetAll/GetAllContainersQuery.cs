using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;

/*
    Search = part of the container number (e.g. "MSCU" or the full number), case-insensitive;
    results are ranked exact match, then prefix, then the rest.
    OperatorId = only containers whose confirmed operating assignment covers today with that operator
    (feedback 2/3: choosing CMA finds the HMM-owned container CMA rents, never by number prefix).
    Catalog filters (all optional, combined with AND):
      OwnerId = owner partner; Category / Size / Condition = master data fields;
      InYard = true: has an open visit, false: not in the yard (never in, released, at an outside facility);
      HasOperator = false: no confirmed operating assignment covers today (gate-in would be refused).
*/
public sealed record GetAllContainersQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    Guid? OperatorId = null,
    Guid? OwnerId = null,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    ContainerCondition? Condition = null,
    bool? InYard = null,
    bool? HasOperator = null)
    : IQuery<Result<PagedResult<ContainerResponse>>>;
