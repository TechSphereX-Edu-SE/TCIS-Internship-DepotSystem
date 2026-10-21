using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;

public sealed class GateOutValidator : AbstractValidator<GateOutCommand>
{
    public GateOutValidator()
    {
        RuleFor(x => x.ContainerId).NotEmpty();
        RuleFor(x => x.DeliveryOrderId).NotEmpty();

        // Column length follows ContainerVisitConfiguration (GateOutVehicleNo)
        RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(20);
    }
}
