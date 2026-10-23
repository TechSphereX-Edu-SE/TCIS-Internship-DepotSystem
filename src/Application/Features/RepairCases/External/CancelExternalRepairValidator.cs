using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

public sealed class CancelExternalRepairValidator : AbstractValidator<CancelExternalRepairCommand>
{
    public CancelExternalRepairValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
