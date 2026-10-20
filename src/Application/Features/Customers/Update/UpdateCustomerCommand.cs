using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Update;

// Only the name can change; the tax code identifies the customer on delivery orders
public sealed record UpdateCustomerCommand(Guid Id, string Name) : ICommand;
