using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;

public sealed class CreateDeliveryOrderValidator : AbstractValidator<CreateDeliveryOrderCommand>
{
    public CreateDeliveryOrderValidator()
    {
        // Column length follows DeliveryOrderConfiguration; letters, digits and - / . as used on order papers
        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[A-Za-z0-9\-/.]+$")
            .WithMessage("Order number may only contain letters, digits, '-', '/' and '.'.");

        RuleFor(x => x.LineOperatorId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ExpiryDate).NotEmpty();
        RuleFor(x => x.VesselVoyage).NotEmpty().MaximumLength(50);

        // At least one container type; each type once (one order line per type)
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("The order must have at least one container type and quantity.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Category).IsInEnum();
            line.RuleFor(l => l.Quantity).GreaterThanOrEqualTo(1);
        });
        RuleFor(x => x.Lines)
            .Must(lines => lines.Select(l => l.Category).Distinct().Count() == lines.Count)
            .When(x => x.Lines is { Count: > 0 })
            .WithMessage("Each container type can appear only once in an order.");
    }
}
