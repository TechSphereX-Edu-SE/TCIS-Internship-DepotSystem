using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;

public sealed class CreateLineOperatorValidator : AbstractValidator<CreateLineOperatorCommand>
{
    public CreateLineOperatorValidator()
    {
        // Short operator code such as MSC, CMA, HMM
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{2,10}$")
            .WithMessage("Code must be 2-10 letters or digits (e.g. MSC, CMA, HMM).");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // Vietnamese tax code: 10 digits, or 10 digits + "-" + 3 digits for a branch; optional
        RuleFor(x => x.TaxCode)
            .Matches(PartnerRules.TaxCodePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode))
            .WithMessage("Tax code must be 10 digits, or 10 digits-3 digits for a branch.");

        RuleFor(x => x)
            .Must(x => x.IsOwner || x.IsOperator)
            .WithName("Roles")
            .WithMessage("A partner must be an owner, an operator, or both.");
    }
}

// Shared by the create and update validators
public static class PartnerRules
{
    public const string TaxCodePattern = @"^\s*\d{10}(-\d{3})?\s*$";
}
