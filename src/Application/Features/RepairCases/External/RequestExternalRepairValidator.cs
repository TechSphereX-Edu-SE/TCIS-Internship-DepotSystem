using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

public sealed class RequestExternalRepairValidator : AbstractValidator<RequestExternalRepairCommand>
{
    public RequestExternalRepairValidator()
    {
        // Column lengths follow ExternalRepairTransferConfiguration
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Facility).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}
