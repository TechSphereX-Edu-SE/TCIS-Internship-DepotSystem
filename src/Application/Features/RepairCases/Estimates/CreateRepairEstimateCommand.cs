using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    Feedback 4: a new estimate version of a case (draft). Version 1 starts empty; a later version starts
    as a copy of the previous one (after a refusal, a change request, or extra damage found while repairing).
*/
public sealed record CreateRepairEstimateCommand(Guid CaseId, string Currency, string? Note, string? Evidence)
    : ICommand<Result<Guid>>;
