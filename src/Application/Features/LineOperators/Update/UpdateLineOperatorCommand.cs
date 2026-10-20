using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

// Only the name can change; the code identifies the operator in statistics and orders
public sealed record UpdateLineOperatorCommand(Guid Id, string Name) : ICommand;
