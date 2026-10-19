using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Create;

public sealed class CreateContainerValidator : AbstractValidator<CreateContainerCommand>
{
    private static readonly string[] AllowedTypes = ["20GP", "40GP", "40HC", "20RF", "40RF"];

    public CreateContainerValidator()
    {
        RuleFor(x => x.ContainerNumber)
            .NotEmpty()
            .Matches("^[A-Za-z]{4}[0-9]{7}$")
            .WithMessage("Container number must be 4 letters + 7 digits (e.g. MSKU1234567).");

        RuleFor(x => x.ContainerType)
            .NotEmpty()
            .Must(t => AllowedTypes.Contains(t.Trim().ToUpperInvariant()))
            .WithMessage($"Container type must be one of: {string.Join(", ", AllowedTypes)}.");
    }
}
