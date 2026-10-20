using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;

public sealed record CreateLineOperatorCommand(string Code, string Name)
    : ICommand<Result<CreateLineOperatorResponse>>;

public sealed record CreateLineOperatorResponse(Guid Id, string Code, string Name);
