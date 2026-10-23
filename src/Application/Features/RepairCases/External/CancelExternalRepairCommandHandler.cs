using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.External;

// 409 ExternalRepair.NotRequested once the container left (or the transfer was already cancelled)
public sealed class CancelExternalRepairCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<CancelExternalRepairCommand>
{
    public async Task<Result> HandleAsync(CancelExternalRepairCommand command, CancellationToken cancellationToken = default)
    {
        var transfer = await dbContext.ExternalRepairTransfers
            .Include(t => t.RepairCase)
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (transfer is null)
            return Result.Failure(NotFound(command.Id));

        var cancelled = transfer.Cancel(transfer.RepairCase, command.Reason, timeProvider.GetUtcNow(), currentUser.Email);
        if (cancelled.IsFailure)
            return Result.Failure(cancelled.Error!);

        dbContext.RepairCaseEvents.Add(cancelled.Value!);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static Error NotFound(Guid id) =>
        Error.NotFound("ExternalRepair.NotFound", $"External repair transfer with ID '{id}' was not found.");
}
