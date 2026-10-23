using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

// Withdraws a transfer that has not left yet; the reason is kept on the transfer and in the case history
public sealed record CancelExternalRepairCommand(Guid Id, string Reason) : ICommand;
