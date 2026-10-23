using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Report;

public sealed class ReportDamageValidator : AbstractValidator<ReportDamageCommand>
{
    public ReportDamageValidator()
    {
        RuleFor(x => x.ContainerId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Source).IsInEnum()
            .NotEqual(RepairFindingSource.GateIn).WithMessage("Gate-in records its damage with the gate-in itself.");
    }
}
