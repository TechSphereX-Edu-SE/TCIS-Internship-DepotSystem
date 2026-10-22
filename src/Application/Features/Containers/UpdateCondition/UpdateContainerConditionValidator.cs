using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;

public sealed class UpdateContainerConditionValidator : AbstractValidator<UpdateContainerConditionCommand>
{
    public UpdateContainerConditionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Condition).IsInEnum();

        // Column length follows ContainerConfiguration
        RuleFor(x => x.ConditionNote).MaximumLength(500);
    }
}
