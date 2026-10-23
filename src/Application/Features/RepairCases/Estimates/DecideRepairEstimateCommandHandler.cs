using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    Approve: the version and the case are saved together; the first approved version moves the case
    from "Chờ duyệt dự toán" to "Đã duyệt sửa" (RepairCase.ApproveEstimate), so the repair is never
    approved without its cost. Reject / RequestChanges: the case keeps waiting; staff make the next version.
    404 RepairEstimate.NotFound; 409 RepairCase.Closed / RepairEstimate.NotSubmitted;
    400 RepairEstimate.ApproverRequired / ReasonRequired. Two decisions at once -> 409 Data.ConcurrentUpdate (xmin).
*/
public sealed class DecideRepairEstimateCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<DecideRepairEstimateCommand>
{
    public async Task<Result> HandleAsync(DecideRepairEstimateCommand command, CancellationToken cancellationToken = default)
    {
        var found = await dbContext.FindEstimateAsync(command.EstimateId, cancellationToken);
        if (found is not { } loaded)
            return Result.Failure(RepairEstimateLoader.NotFound(command.EstimateId));
        var (estimate, repairCase) = loaded;
        if (!repairCase.IsOpen)
            return Result.Failure(RepairCase.Closed());

        var now = timeProvider.GetUtcNow();
        var decided = estimate.Decide(command.Decision, command.ApproverParty, command.ApproverName,
            command.ApprovalReference, command.Note, now, currentUser.Email);
        if (decided.IsFailure)
            return decided;

        RepairCaseEvent history;
        if (command.Decision == RepairEstimateDecision.Approve)
        {
            // The case is open (checked above) and the version is now Approved, so this cannot fail
            var approved = repairCase.ApproveEstimate(estimate, now, currentUser.Email);
            if (approved.IsFailure)
                return Result.Failure(approved.Error!);
            history = approved.Value!;
        }
        else
        {
            var kind = command.Decision == RepairEstimateDecision.Reject
                ? RepairEventKind.EstimateRejected
                : RepairEventKind.EstimateChangesRequested;
            history = repairCase.Record(kind, estimate.HistoryText(estimate.DecisionNote), now, currentUser.Email);
        }

        dbContext.RepairCaseEvents.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
