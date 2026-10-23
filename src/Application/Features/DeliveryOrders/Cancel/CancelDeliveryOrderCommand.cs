using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Cancel;

// Q7: cancels an order nothing was released under; the reason is kept with the order
public sealed record CancelDeliveryOrderCommand(Guid Id, string Reason) : ICommand;
