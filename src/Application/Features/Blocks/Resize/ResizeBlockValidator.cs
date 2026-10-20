using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;

public sealed class ResizeBlockValidator : AbstractValidator<ResizeBlockCommand>
{
    public ResizeBlockValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.MaxBays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.MaxRows).GreaterThanOrEqualTo(1);
        RuleFor(x => x.MaxTiers).GreaterThanOrEqualTo(1);
    }
}
