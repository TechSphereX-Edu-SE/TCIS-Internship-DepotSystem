using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

/*
    External repair transfers, newest request first. The gate-out page lists Status = Requested
    (waiting to leave); Search = part of the container number.
*/
public sealed record GetExternalRepairsQuery(ExternalRepairStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<ExternalRepairResponse>>>;
