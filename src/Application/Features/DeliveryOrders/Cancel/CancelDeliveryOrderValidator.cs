using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Cancel;

public sealed class CancelDeliveryOrderValidator : AbstractValidator<CancelDeliveryOrderCommand>
{
    public CancelDeliveryOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // Column length follows DeliveryOrderConfiguration (CancelReason)
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
