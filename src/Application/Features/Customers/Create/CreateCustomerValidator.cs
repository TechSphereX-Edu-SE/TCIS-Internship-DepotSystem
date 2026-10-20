using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.Create;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        // Vietnamese tax code: 10 digits, or 10 digits + "-" + 3 digits for a branch
        RuleFor(x => x.TaxCode)
            .NotEmpty()
            .Matches(@"^\d{10}(-\d{3})?$")
            .WithMessage("Tax code must be 10 digits, or 10 digits + '-' + 3 digits (e.g. 0301234567, 0301234567-001).");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
