using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    Feedback 4: who pays one item, and how far that is settled (Expected -> Agreed, or Disputed with a reason).
    Allowed in every version status and even after the case is closed: the allocation is often settled
    after the repair. Amounts never change here.
*/
public sealed record SetEstimateLinePayerCommand(
    Guid EstimateId, Guid LineId, CostPayer Payer, string? PayerName, CostPayerStatus Status, string? Note)
    : ICommand;
