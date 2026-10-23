using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

/*
    Name, tax code and roles can change; the code identifies the partner in statistics and orders.
    The role defaults match the old "rename" request (operator only).
*/
public sealed record UpdateLineOperatorCommand(
    Guid Id, string Name, string? TaxCode = null, bool IsOwner = false, bool IsOperator = true) : ICommand;
