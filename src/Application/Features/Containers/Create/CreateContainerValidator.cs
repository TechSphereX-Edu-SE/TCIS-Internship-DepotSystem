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
            .WithMessage("Container number must be 4 letters + 6 digits + a valid check digit (0-9, " +
                         "or X when the Modulo 11 remainder is 10), e.g. CSQU3054383, CMAU100000X.");
        RuleFor(x => x.IsoCode)
            .NotEmpty()
            .Matches("^[A-Za-z0-9]{4}$")
            .WithMessage("ISO code must be 4 letters/digits (e.g. 22G1, 45R1).");

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
