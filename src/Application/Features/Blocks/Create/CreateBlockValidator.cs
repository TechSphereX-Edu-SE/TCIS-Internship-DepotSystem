using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Entities;
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

        // At least one container type, each a known type (duplicates are simply merged by the domain)
        RuleFor(x => x.AllowedCategories)
            .NotEmpty()
            .WithMessage("Choose at least one container type for the block.");
        RuleForEach(x => x.AllowedCategories).IsInEnum();

        // Physical block: every dimension is required; tiers stack "from 2 to many" (spec section 1)
        When(x => !x.IsVirtual, () =>
        {
            RuleFor(x => x.MaxBays).NotNull().GreaterThanOrEqualTo(1);
            RuleFor(x => x.MaxRows).NotNull().GreaterThanOrEqualTo(1);
            RuleFor(x => x.MaxTiers).NotNull().GreaterThanOrEqualTo(Block.MinTiers);
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
