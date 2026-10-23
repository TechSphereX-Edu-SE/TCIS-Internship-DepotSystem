using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

public sealed class CreateRepairEstimateValidator : AbstractValidator<CreateRepairEstimateCommand>
{
    public CreateRepairEstimateValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Currency).EstimateCurrency();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Evidence).MaximumLength(1000);
    }
}
