using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;

public sealed class CreateBlockValidator : AbstractValidator<CreateBlockCommand>
{
    public CreateBlockValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{1,10}$")
            .WithMessage("Code must be 1-10 letters or digits (e.g. A, B1, WAIT).");

        RuleFor(x => x.Description).MaximumLength(200);

        // Physical block: every dimension is required and at least 1
        When(x => !x.IsVirtual, () =>
        {
            RuleFor(x => x.MaxBays).NotNull().GreaterThanOrEqualTo(1);
            RuleFor(x => x.MaxRows).NotNull().GreaterThanOrEqualTo(1);
            RuleFor(x => x.MaxTiers).NotNull().GreaterThanOrEqualTo(1);
        });

        // Virtual block: not managed by bay/row/tier
        When(x => x.IsVirtual, () =>
        {
            RuleFor(x => x.MaxBays).Null().WithMessage("A virtual block has no bays.");
            RuleFor(x => x.MaxRows).Null().WithMessage("A virtual block has no rows.");
            RuleFor(x => x.MaxTiers).Null().WithMessage("A virtual block has no tiers.");
        });
    }
}
