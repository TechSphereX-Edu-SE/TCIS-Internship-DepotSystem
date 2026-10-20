using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;

public sealed record GetAllLineOperatorsQuery(int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<LineOperatorResponse>>>;
