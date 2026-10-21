using FluentValidation;
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
        RuleFor(x => x.BlockId).NotEmpty();

        // Positions start at 1 (same as CK_ContainerVisits_Position)
        RuleFor(x => x.Bay).GreaterThanOrEqualTo(1).When(x => x.Bay is not null);
        RuleFor(x => x.Row).GreaterThanOrEqualTo(1).When(x => x.Row is not null);
        RuleFor(x => x.Tier).GreaterThanOrEqualTo(1).When(x => x.Tier is not null);

        // Bay/Row/Tier are all given (physical block) or all omitted (virtual block)
        RuleFor(x => x)
            .Must(x => (x.Bay, x.Row, x.Tier) is (null, null, null) ||
                       (x.Bay is not null && x.Row is not null && x.Tier is not null))
            .WithName("Position")
            .WithMessage("Bay, row and tier must be all given or all omitted.");

        RuleFor(x => x.Grade).IsInEnum();
        RuleFor(x => x.Condition).IsInEnum();

        // Column lengths follow ContainerVisitConfiguration
        RuleFor(x => x.ConditionNote).MaximumLength(500);

        RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(20);
    }
}
