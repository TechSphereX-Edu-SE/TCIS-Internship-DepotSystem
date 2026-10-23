using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;

/*
    Search = part of the code or the name, case-insensitive (searchable dropdowns); ranked like containers.
    Role = only partners with that role (owner dropdown of the container catalog, operator dropdowns).
*/
public sealed record GetAllLineOperatorsQuery(int Page = 1, int PageSize = 20, string? Search = null, PartnerRole? Role = null)
    : IQuery<Result<PagedResult<LineOperatorResponse>>>;
