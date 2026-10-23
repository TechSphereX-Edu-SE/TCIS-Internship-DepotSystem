using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// 404 RepairEstimate.NotFound / LineNotFound; 400 PayerNameRequired / DisputeReasonRequired / PayerUndetermined
public sealed class SetEstimateLinePayerCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser currentUser)
    : ICommandHandler<SetEstimateLinePayerCommand>
{
    public async Task<Result> HandleAsync(SetEstimateLinePayerCommand command, CancellationToken cancellationToken = default)
    {
        var found = await dbContext.FindEstimateAsync(command.EstimateId, cancellationToken);
        if (found is not { } loaded)
            return Result.Failure(RepairEstimateLoader.NotFound(command.EstimateId));

        var set = loaded.Estimate.SetPayer(command.LineId, command.Payer, command.PayerName, command.Status, command.Note);
        if (set.IsFailure)
            return Result.Failure(set.Error!);

        // History: "v2 #3: Owner (ABC) · Disputed · reason"
        var line = set.Value!;
        var party = line.PayerName is null ? line.Payer.ToString() : $"{line.Payer} ({line.PayerName})";
        var text = $"v{loaded.Estimate.Version} #{line.Position}: {party} · {line.PayerStatus}";
        if (line.PayerNote is not null)
            text += $" · {line.PayerNote}";
        dbContext.RepairCaseEvents.Add(loaded.Case.Record(RepairEventKind.PayerChanged,
            text.Length <= 500 ? text : text[..497] + "...", timeProvider.GetUtcNow(), currentUser.Email));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
