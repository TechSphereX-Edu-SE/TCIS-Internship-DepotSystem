using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOutForRepair;

public sealed class GateOutForRepairValidator : AbstractValidator<GateOutForRepairCommand>
{
    public GateOutForRepairValidator()
    {
        RuleFor(x => x.TransferId).NotEmpty();
        // Column length follows ContainerVisitConfiguration (GateOutVehicleNo)
        RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(20);
    }
}
