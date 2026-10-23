using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    404 RepairCase.NotFound; 409 RepairCase.Closed / RepairEstimate.NotInspected / RepairEstimate.OpenVersion (domain).
    Two versions created at the same moment hit the unique index (case, version) -> 409 Data.ConcurrentUpdate.
*/
public sealed class CreateRepairEstimateCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<CreateRepairEstimateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateRepairEstimateCommand command, CancellationToken cancellationToken = default)
    {
        var repairCase = await dbContext.RepairCases.FirstOrDefaultAsync(c => c.Id == command.CaseId, cancellationToken);
        if (repairCase is null)
            return Result.Failure<Guid>(ApplyRepairActionCommandHandler.NotFound(command.CaseId));

        var previous = await dbContext.RepairEstimates
            .Include(e => e.Lines)
            .Where(e => e.RepairCaseId == repairCase.Id)
            .OrderByDescending(e => e.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var created = RepairEstimate.Create(repairCase, previous, command.Currency, command.Note, command.Evidence);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error!);

        var estimate = created.Value!;
        dbContext.RepairEstimates.Add(estimate);
        dbContext.RepairCaseEvents.Add(repairCase.Record(RepairEventKind.EstimateCreated,
            estimate.HistoryText(previous is null ? null : $"copy of v{previous.Version}"),
            timeProvider.GetUtcNow(), currentUser.Email));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(estimate.Id);
    }
}
