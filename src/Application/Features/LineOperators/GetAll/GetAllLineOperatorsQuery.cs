using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;

// Search = part of the code or the name, case-insensitive (searchable dropdowns); ranked like containers
public sealed record GetAllLineOperatorsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IQuery<Result<PagedResult<LineOperatorResponse>>>;
