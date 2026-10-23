using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

public sealed class UpdateRepairEstimateValidator : AbstractValidator<UpdateRepairEstimateCommand>
{
    public UpdateRepairEstimateValidator()
    {
        RuleFor(x => x.EstimateId).NotEmpty();
        RuleFor(x => x.Currency).EstimateCurrency();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Evidence).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull()
            .Must(l => l is null || l.Count <= RepairEstimate.MaxLines)
            .WithMessage($"At most {RepairEstimate.MaxLines} items per estimate.");
        RuleForEach(x => x.Lines).SetValidator(new RepairEstimateLineInputValidator());
    }
}
