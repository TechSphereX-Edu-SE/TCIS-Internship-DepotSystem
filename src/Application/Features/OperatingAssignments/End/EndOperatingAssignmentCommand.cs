using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.End;

// Sets the last day (Vietnam date, included) of a confirmed assignment, e.g. when the lease ends
public sealed record EndOperatingAssignmentCommand(Guid Id, DateOnly ValidTo) : ICommand;
