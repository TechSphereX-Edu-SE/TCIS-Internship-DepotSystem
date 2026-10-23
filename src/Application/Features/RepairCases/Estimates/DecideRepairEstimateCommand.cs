using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    Feedback 4: records the answer of the approving party on a submitted version.
    ApproverParty = who decided (e.g. the line operator), ApproverName = the person,
    ApprovalReference = the e-mail / document number. Note = the reason of a refusal or of the changes asked.
*/
public sealed record DecideRepairEstimateCommand(
    Guid EstimateId,
    RepairEstimateDecision Decision,
    string? ApproverParty,
    string? ApproverName,
    string? ApprovalReference,
    string? Note)
    : ICommand;
