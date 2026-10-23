using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

public sealed class SetEstimateLinePayerValidator : AbstractValidator<SetEstimateLinePayerCommand>
{
    public SetEstimateLinePayerValidator()
    {
        RuleFor(x => x.EstimateId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Payer).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.PayerName).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
