using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Get;

public sealed record GetCustomerQuery(Guid Id) : IQuery<Result<CustomerResponse>>;

public sealed record CustomerResponse(Guid Id, string TaxCode, string Name, DateTimeOffset CreatedAt)
{
    public static CustomerResponse From(Customer c) => new(c.Id, c.TaxCode, c.Name, c.CreatedAt);
}
