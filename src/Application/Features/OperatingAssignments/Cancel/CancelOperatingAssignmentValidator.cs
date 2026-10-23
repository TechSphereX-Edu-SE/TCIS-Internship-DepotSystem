using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Cancel;

public sealed class CancelOperatingAssignmentValidator : AbstractValidator<CancelOperatingAssignmentCommand>
{
    public CancelOperatingAssignmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
