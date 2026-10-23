using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;

public sealed record GetLineOperatorQuery(Guid Id) : IQuery<Result<LineOperatorResponse>>;

// Partner (owner and/or line operator, Q2); the API path stays /api/line-operators
public sealed record LineOperatorResponse(
    Guid Id, string Code, string Name, string? TaxCode, bool IsOwner, bool IsOperator, DateTimeOffset CreatedAt)
{
    public static LineOperatorResponse From(LineOperator l) =>
        new(l.Id, l.Code, l.Name, l.TaxCode, l.IsOwner, l.IsOperator, l.CreatedAt);
}
