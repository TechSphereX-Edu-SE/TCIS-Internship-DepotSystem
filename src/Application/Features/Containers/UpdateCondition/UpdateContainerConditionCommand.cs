using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;

/*
    Records the current condition of a container (spec section 2 "Container Condition").
    Used e.g. after a damaged container is repaired, so it can be released again at gate-out.
    The condition recorded at gate-in stays on the visit (ConditionIn) as history.
*/
public sealed record UpdateContainerConditionCommand(
    Guid Id,
    ContainerCondition Condition,
    string? ConditionNote) : ICommand;
