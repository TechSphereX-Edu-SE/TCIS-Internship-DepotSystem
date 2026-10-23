using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;

/*
    Creates a delivery order with its lines.
    - The expiry date cannot be before today (Vietnam date): such an order could never be used.
    - Line operator and customer must exist (404); the order number is unique (409).
    - Lines are added through the domain (DeliveryOrder.AddLine), which also guards quantity
      and duplicate types.
*/
public sealed class CreateDeliveryOrderCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<CreateDeliveryOrderCommand, Result<CreateDeliveryOrderResponse>>
{
    public async Task<Result<CreateDeliveryOrderResponse>> HandleAsync(
        CreateDeliveryOrderCommand command, CancellationToken cancellationToken = default)
    {
        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        if (command.ExpiryDate < today)
            return Result.Failure<CreateDeliveryOrderResponse>(Error.Validation("DeliveryOrder.AlreadyExpired",
                $"Expiry date {command.ExpiryDate:dd/MM/yyyy} is before today ({today:dd/MM/yyyy})."));

        var lineOperator = await dbContext.LineOperators
            .FirstOrDefaultAsync(l => l.Id == command.LineOperatorId, cancellationToken);
        if (lineOperator is null)
            return Result.Failure<CreateDeliveryOrderResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{command.LineOperatorId}' was not found."));

        // A delivery order is issued by the line operator, so an owner-only partner (e.g. a leasing company) cannot
        if (!lineOperator.IsOperator)
            return Result.Failure<CreateDeliveryOrderResponse>(Error.Conflict("Partner.NotOperator",
                $"Partner {lineOperator.Code} does not have the line operator role."));

        if (!await dbContext.Customers.AnyAsync(c => c.Id == command.CustomerId, cancellationToken))
            return Result.Failure<CreateDeliveryOrderResponse>(Error.NotFound("Customer.NotFound",
                $"Customer with ID '{command.CustomerId}' was not found."));

        var order = new DeliveryOrder(
            command.OrderNumber, command.LineOperatorId, command.CustomerId, command.ExpiryDate, command.VesselVoyage);

        // Compare with the normalized number (the entity trims and upper-cases it)
        if (await dbContext.DeliveryOrders.AnyAsync(o => o.OrderNumber == order.OrderNumber, cancellationToken))
            return Result.Failure<CreateDeliveryOrderResponse>(Error.Conflict("DeliveryOrder.Duplicate",
                $"Delivery order '{order.OrderNumber}' already exists."));

        foreach (var line in command.Lines)
        {
            var added = order.AddLine(line.Category, line.Quantity, line.Size);
            if (added.IsFailure)
                return Result.Failure<CreateDeliveryOrderResponse>(added.Error!);
        }

        dbContext.DeliveryOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateDeliveryOrderResponse(order.Id, order.OrderNumber));
    }
}
