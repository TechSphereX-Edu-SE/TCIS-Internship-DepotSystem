using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

public sealed class UpdateLineOperatorValidator : AbstractValidator<UpdateLineOperatorCommand>
{
    public UpdateLineOperatorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

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
