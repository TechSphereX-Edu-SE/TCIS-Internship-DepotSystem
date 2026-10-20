using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Create;

public sealed record CreateCustomerCommand(string TaxCode, string Name)
    : ICommand<Result<CreateCustomerResponse>>;

public sealed record CreateCustomerResponse(Guid Id, string TaxCode, string Name);
