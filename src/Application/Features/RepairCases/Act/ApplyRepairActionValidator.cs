using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;

public sealed class ApplyRepairActionValidator : AbstractValidator<ApplyRepairActionCommand>
{
    public ApplyRepairActionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
