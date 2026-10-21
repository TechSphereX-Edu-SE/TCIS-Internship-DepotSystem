using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;

public sealed class MoveContainerValidator : AbstractValidator<MoveContainerCommand>
{
    public MoveContainerValidator()
    {
        RuleFor(x => x.ContainerId).NotEmpty();

        // Same block and Bay/Row/Tier rules as gate-in
        Include(new YardPositionValidator());
    }
}
