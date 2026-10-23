using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Cancel;

// Withdraws an assignment recorded by mistake; the reason is kept with it
public sealed record CancelOperatingAssignmentCommand(Guid Id, string Reason) : ICommand;
