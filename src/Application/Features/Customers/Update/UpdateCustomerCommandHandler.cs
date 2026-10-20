using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Update;

public sealed class UpdateCustomerCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateCustomerCommand>
{
    public async Task<Result> HandleAsync(
        UpdateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FindAsync([command.Id], cancellationToken);
        if (customer is null)
            return Result.Failure(Error.NotFound("Customer.NotFound", $"Customer with ID '{command.Id}' was not found."));

        customer.Rename(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
