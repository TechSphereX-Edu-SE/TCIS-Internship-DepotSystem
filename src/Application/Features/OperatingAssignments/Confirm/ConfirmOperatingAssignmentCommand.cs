using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Confirm;

// Pending -> Confirmed, once the relation has been checked against the contract / instruction
public sealed record ConfirmOperatingAssignmentCommand(Guid Id) : ICommand;
