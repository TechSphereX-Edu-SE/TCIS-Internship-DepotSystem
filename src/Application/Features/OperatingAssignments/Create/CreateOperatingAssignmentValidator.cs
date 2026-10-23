using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Create;

public sealed class CreateOperatingAssignmentValidator : AbstractValidator<CreateOperatingAssignmentCommand>
{
    public CreateOperatingAssignmentValidator()
    {
        RuleFor(x => x.ContainerId).NotEmpty();
        RuleFor(x => x.OperatorId).NotEmpty();
        RuleFor(x => x.ValidTo)
            .GreaterThanOrEqualTo(x => x.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x.Reference).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
