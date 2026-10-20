using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Create;

public sealed class CreateCustomerCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<CreateCustomerCommand, Result<CreateCustomerResponse>>
{
    public async Task<Result<CreateCustomerResponse>> HandleAsync(
        CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var taxCode = command.TaxCode.Trim();

        var exists = await dbContext.Customers.AnyAsync(c => c.TaxCode == taxCode, cancellationToken);
        if (exists)
            return Result.Failure<CreateCustomerResponse>(
                Error.Conflict("Customer.Duplicate", $"Customer with tax code '{taxCode}' already exists."));

        var customer = new Customer(taxCode, command.Name);

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateCustomerResponse(customer.Id, customer.TaxCode, customer.Name));
    }
}
