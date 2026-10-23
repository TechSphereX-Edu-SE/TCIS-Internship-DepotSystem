using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Update;

// Same field rules as CreateBlockValidator
public sealed class UpdateBlockValidator : AbstractValidator<UpdateBlockCommand>
{
    public UpdateBlockValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{1,10}$")
            .WithMessage("Code must be 1-10 letters or digits (e.g. A, B1, WAIT).");

        RuleFor(x => x.Description).MaximumLength(200);

        RuleFor(x => x.AllowedCategories)
            .NotEmpty()
            .WithMessage("Choose at least one container type for the block.");
        RuleForEach(x => x.AllowedCategories).IsInEnum();
    }
}
