using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    404 RepairEstimate.NotFound; 409 RepairCase.Closed / RepairEstimate.NotDraft;
    400 RepairEstimate.UnknownFinding when an item points to a damage of another case.
    Saving a draft is not written to the case history (only submit and decisions are); the audit
    columns keep who changed it last.
*/
public sealed class UpdateRepairEstimateCommandHandler(IAppDbContext dbContext)
    : ICommandHandler<UpdateRepairEstimateCommand>
{
    public async Task<Result> HandleAsync(UpdateRepairEstimateCommand command, CancellationToken cancellationToken = default)
    {
        var found = await dbContext.FindEstimateAsync(command.EstimateId, cancellationToken);
        if (found is not { } loaded)
            return Result.Failure(RepairEstimateLoader.NotFound(command.EstimateId));
        if (!loaded.Case.IsOpen)
            return Result.Failure(RepairCase.Closed());

        // An item may name the damage it costs, but only a damage of this case
        var findingIds = command.Lines.Where(l => l.FindingId is not null).Select(l => l.FindingId!.Value).Distinct().ToList();
        if (findingIds.Count > 0)
        {
            var known = await dbContext.RepairFindings
                .CountAsync(f => f.RepairCaseId == loaded.Case.Id && findingIds.Contains(f.Id), cancellationToken);
            if (known != findingIds.Count)
                return Result.Failure(Error.Validation("RepairEstimate.UnknownFinding",
                    "An item refers to a damage that is not part of this repair case."));
        }

        var updated = loaded.Estimate.UpdateDraft(command.Currency, command.Note, command.Evidence,
            command.Lines.Select(l => (l.FindingId, l.DamageDescription, l.RepairMethod, l.Amount, l.Payer, l.PayerName)).ToList());
        if (updated.IsFailure)
            return updated;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
