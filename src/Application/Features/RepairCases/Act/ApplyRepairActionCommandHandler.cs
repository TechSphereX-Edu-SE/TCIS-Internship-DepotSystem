using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;

/*
    Moves a case one step (RepairCase.Apply holds the rules) and keeps a history row with who and when.
    Passed sets the container back to Normal, which only lifts the repair block: the container is
    neither released nor moved (feedback 4).
*/
public sealed class ApplyRepairActionCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<ApplyRepairActionCommand>
{
    public async Task<Result> HandleAsync(ApplyRepairActionCommand command, CancellationToken cancellationToken = default)
    {
        var repairCase = await dbContext.RepairCases
            .Include(c => c.Container)
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (repairCase is null)
            return Result.Failure(NotFound(command.Id));

        var applied = repairCase.Apply(command.Action, command.Note, repairCase.Container, timeProvider.GetUtcNow(), currentUser.Email);
        if (applied.IsFailure)
            return Result.Failure(applied.Error!);

        dbContext.RepairCaseEvents.Add(applied.Value!);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static Error NotFound(Guid id) =>
        Error.NotFound("RepairCase.NotFound", $"Repair case with ID '{id}' was not found.");
}
