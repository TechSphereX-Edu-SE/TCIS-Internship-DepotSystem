using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// The domain repeats the two "required" rules, so a call that skips this validator still gets them
public sealed class DecideRepairEstimateValidator : AbstractValidator<DecideRepairEstimateCommand>
{
    public DecideRepairEstimateValidator()
    {
        RuleFor(x => x.EstimateId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.ApproverParty).MaximumLength(200);
        RuleFor(x => x.ApproverParty).NotEmpty()
            .When(x => x.Decision == RepairEstimateDecision.Approve)
            .WithMessage("Name the party that approved the estimate.");
        RuleFor(x => x.ApproverName).MaximumLength(200);
        RuleFor(x => x.ApprovalReference).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Note).NotEmpty()
            .When(x => x.Decision != RepairEstimateDecision.Approve)
            .WithMessage("Give the reason of the refusal or the changes asked.");
    }
}
