using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// Saves a draft: header and the whole item table (items left out are removed)
public sealed record UpdateRepairEstimateCommand(
    Guid EstimateId, string Currency, string? Note, string? Evidence, IReadOnlyList<RepairEstimateLineInput> Lines)
    : ICommand;
