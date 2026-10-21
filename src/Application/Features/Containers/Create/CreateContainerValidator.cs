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
            .WithMessage("Container number must be 4 letters + 6-digit serial (not 000000) + a valid check digit " +
                         "(0-9, or X when the Modulo 11 remainder is 10), e.g. CSQU3054383, CMAU100000X.");
        RuleFor(x => x.IsoCode)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{4}$")
            .WithMessage("ISO code must be 4 letters/digits (e.g. 22G1, 45R1).");

        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();

        // The 4th letter of the number is the type code and must match the container type
        RuleFor(x => x)
            .Must(x => ContainerNumberRules.MatchesCategory(x.ContainerNumber, x.Category))
            .When(x => ContainerNumberRules.IsValid(x.ContainerNumber) && Enum.IsDefined(x.Category))
            .OverridePropertyName(nameof(CreateContainerCommand.Category))
            .WithMessage(x => $"Container type {x.Category} requires type code '{ContainerNumberRules.TypeCodes[x.Category]}' as the 4th letter of the container number.");

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
