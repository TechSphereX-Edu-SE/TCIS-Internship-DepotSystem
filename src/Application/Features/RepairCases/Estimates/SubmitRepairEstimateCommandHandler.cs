using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// 404 RepairEstimate.NotFound; 409 RepairCase.Closed / RepairEstimate.NotDraft; 400 RepairEstimate.NoLines
public sealed class SubmitRepairEstimateCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<SubmitRepairEstimateCommand>
{
    public async Task<Result> HandleAsync(SubmitRepairEstimateCommand command, CancellationToken cancellationToken = default)
    {
        var found = await dbContext.FindEstimateAsync(command.EstimateId, cancellationToken);
        if (found is not { } loaded)
            return Result.Failure(RepairEstimateLoader.NotFound(command.EstimateId));
        if (!loaded.Case.IsOpen)
            return Result.Failure(RepairCase.Closed());

        var now = timeProvider.GetUtcNow();
        var submitted = loaded.Estimate.Submit(now, currentUser.Email);
        if (submitted.IsFailure)
            return submitted;

        dbContext.RepairCaseEvents.Add(loaded.Case.Record(RepairEventKind.EstimateSubmitted,
            loaded.Estimate.HistoryText(), now, currentUser.Email));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
