using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Get;

public sealed class GetCustomerQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetCustomerQuery, Result<CustomerResponse>>
{
    public async Task<Result<CustomerResponse>> HandleAsync(
        GetCustomerQuery query, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);

        return customer is null
            ? Result.Failure<CustomerResponse>(
                Error.NotFound("Customer.NotFound", $"Customer with ID '{query.Id}' was not found."))
            : Result.Success(CustomerResponse.From(customer));
    }
}
