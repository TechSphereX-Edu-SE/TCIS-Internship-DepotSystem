using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Services;

namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Update;

// Same field rules as CreateContainerValidator, without the container number
public sealed class UpdateContainerValidator : AbstractValidator<UpdateContainerCommand>
{
    public UpdateContainerValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        // ISO 6346 size-type code must fit the size and the container type (rules in IsoCodeRules)
        RuleFor(x => x.IsoCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must((x, isoCode) => IsoCodeRules.IsValid(isoCode, x.Size, x.Category))
            .WithMessage(x => IsoCodeRules.Validate(x.IsoCode, x.Size, x.Category)!);

        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();

        RuleFor(x => x.TareWeightKg).GreaterThan(0);
        RuleFor(x => x.MaxGrossWeightKg)
            .GreaterThan(x => x.TareWeightKg)
            .WithMessage("Max gross weight must be greater than tare weight.");

        RuleFor(x => x.ManufactureDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.ManufactureDate.HasValue)
            .WithMessage("Manufacture date cannot be in the future.");

        RuleFor(x => x.OwnerId).NotEmpty();
    }
}
