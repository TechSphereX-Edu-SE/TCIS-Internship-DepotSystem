using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed class CreateContainerValidator : AbstractValidator<CreateContainerCommand>
{
    public CreateContainerValidator()
    {
        RuleFor(x => x.ContainerNumber)
            .NotEmpty()
            .Must(ContainerNumberRules.IsValid)
            .WithMessage("Container number must be ISO 6346: 3-letter owner code + U + 6-digit serial (not 000000) " +
                "+ a valid check digit (0-9, or X when the Modulo 11 remainder is 10), e.g. CSQU3054383, CMAU100000X.");

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

        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(100);
    }
}
