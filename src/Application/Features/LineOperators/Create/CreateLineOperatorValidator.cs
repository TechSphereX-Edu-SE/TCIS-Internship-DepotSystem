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
    }
}
