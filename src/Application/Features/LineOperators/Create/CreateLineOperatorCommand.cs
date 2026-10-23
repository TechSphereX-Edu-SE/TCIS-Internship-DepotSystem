using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;

/*
    Creates a partner (Q2): a company recorded once with its roles. The defaults (operator only)
    keep the meaning of the old "create line operator" request that sent only code and name.
*/
public sealed record CreateLineOperatorCommand(
    string Code, string Name, string? TaxCode = null, bool IsOwner = false, bool IsOperator = true)
    : ICommand<Result<CreateLineOperatorResponse>>;

public sealed record CreateLineOperatorResponse(Guid Id, string Code, string Name);
