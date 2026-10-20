using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;

public sealed class UpdateLineOperatorValidator : AbstractValidator<UpdateLineOperatorCommand>
{
    public UpdateLineOperatorValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
