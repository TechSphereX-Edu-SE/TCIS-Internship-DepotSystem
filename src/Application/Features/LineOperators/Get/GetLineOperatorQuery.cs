using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;

public sealed record GetLineOperatorQuery(Guid Id) : IQuery<Result<LineOperatorResponse>>;

public sealed record LineOperatorResponse(Guid Id, string Code, string Name, DateTimeOffset CreatedAt)
{
    public static LineOperatorResponse From(LineOperator l) => new(l.Id, l.Code, l.Name, l.CreatedAt);
}
