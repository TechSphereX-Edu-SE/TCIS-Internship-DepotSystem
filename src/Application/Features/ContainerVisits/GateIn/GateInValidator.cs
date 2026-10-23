using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;

/*
    Shape checks only. Whether the position fits the block depends on the block
    (physical or virtual, its size) and the container size, so the domain checks that.
*/
public sealed class GateInValidator : AbstractValidator<GateInCommand>
{
    public GateInValidator()
    {
        RuleFor(x => x.ContainerId).NotEmpty();
        RuleFor(x => x.LineOperatorId).NotEmpty();

        // Block and Bay/Row/Tier rules are shared with move
        Include(new YardPositionValidator());

        RuleFor(x => x.Condition).IsInEnum();

        // Column lengths follow ContainerVisitConfiguration
        RuleFor(x => x.ConditionNote).MaximumLength(500);

        // Feedback 3: a damage seen at the gate is described; it becomes the first finding of a repair case
        RuleFor(x => x.ConditionNote)
            .NotEmpty()
            .When(x => x.Condition == ContainerCondition.Damaged)
            .WithMessage("Describe the damage seen at the gate.");

        RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(20);
    }
}
