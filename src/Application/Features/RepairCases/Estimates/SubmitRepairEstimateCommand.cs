using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// Locks the draft and sends it for approval (the approving party answers outside the system)
public sealed record SubmitRepairEstimateCommand(Guid EstimateId) : ICommand;
