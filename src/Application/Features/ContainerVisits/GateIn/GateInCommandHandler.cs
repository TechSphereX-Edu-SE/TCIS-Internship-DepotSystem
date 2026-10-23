using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;

/*
    Brings a container into the yard.
    Order of checks:
      1. Container, line operator and block exist (404).
      2. The container is not already in the yard (409) - one open visit per container.
      2b. Feedback 2: a confirmed operating assignment covers today (Vietnam date) and names the chosen
         line operator (409 otherwise). The operator is never guessed from the owner, the number prefix
         or an earlier visit; missing or conflicting data must be verified first. The visit keeps the
         assignment as its basis.
      3. The block takes this container type (409) and the position fits the block and the
         container size (400) - done by the domain (ContainerVisit.GateIn -> Block.CheckAccepts,
         Block.ValidateSlot), so the rules live in one place.
      4. The position is free and supported (409) - YardPlacement / YardSlotRules: no overlap with
         20ft/40ft footprints in the same row and tier, and something underneath above tier 1.
*/
public sealed class GateInCommandHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : ICommandHandler<GateInCommand, Result<GateInResponse>>
{
    public async Task<Result<GateInResponse>> HandleAsync(
        GateInCommand command, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers.FindAsync([command.ContainerId], cancellationToken);
        if (container is null)
            return Result.Failure<GateInResponse>(Error.NotFound("Container.NotFound",
                $"Container with ID '{command.ContainerId}' was not found."));

        var lineOperatorExists = await dbContext.LineOperators
            .AnyAsync(l => l.Id == command.LineOperatorId, cancellationToken);
        if (!lineOperatorExists)
            return Result.Failure<GateInResponse>(Error.NotFound("LineOperator.NotFound",
                $"Line operator with ID '{command.LineOperatorId}' was not found."));

        var block = await dbContext.Blocks.FindAsync([command.BlockId], cancellationToken);
        if (block is null)
            return Result.Failure<GateInResponse>(Error.NotFound("Block.NotFound",
                $"Block with ID '{command.BlockId}' was not found."));

        if (await IsInYardAsync(container.Id, cancellationToken))
            return Result.Failure<GateInResponse>(Error.Conflict("ContainerVisit.AlreadyInYard",
                $"Container {container.ContainerNumber} is already in the yard."));

        var now = timeProvider.GetUtcNow();
        var today = DepotTime.DateOf(now);
        var basis = await dbContext.FindActiveAsync(container.Id, today, cancellationToken);
        if (basis is null)
            return Result.Failure<GateInResponse>(Error.Conflict("ContainerVisit.NoOperatingAssignment",
                $"Container {container.ContainerNumber} has no confirmed operating assignment on {today:dd/MM/yyyy}. " +
                "Record and confirm which line operator operates it first."));

        if (basis.OperatorId != command.LineOperatorId)
            return Result.Failure<GateInResponse>(Error.Conflict("ContainerVisit.OperatorMismatch",
                $"Container {container.ContainerNumber} is operated by {basis.Operator.Code} on {today:dd/MM/yyyy} " +
                "according to its confirmed operating assignment."));

        // The domain validates the slot (range, bay parity, 40ft footprint) before creating the visit.
        // Nothing is saved on failure, so the condition it sets on the tracked container is discarded.
        var gateIn = ContainerVisit.GateIn(
            container, command.LineOperatorId, block,
            command.Bay, command.Row, command.Tier,
            command.Condition, command.ConditionNote,
            command.VehicleNo, now, basis);
        if (gateIn.IsFailure)
            return Result.Failure<GateInResponse>(gateIn.Error!);

        // A virtual block has no slots, so any number of containers can be in it
        if (!block.IsVirtual)
        {
            var placement = await dbContext.CheckPlacementAsync(
                block, command.Bay!.Value, command.Row!.Value, command.Tier!.Value, container.Size,
                excludeVisitId: null, cancellationToken);
            if (placement.IsFailure)
                return Result.Failure<GateInResponse>(placement.Error!);
        }

        var visit = gateIn.Value!;
        dbContext.ContainerVisits.Add(visit);

        // The partial unique indexes (one open visit per container, one container per slot)
        // still guard the database if two gate-ins race past the checks above.
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new GateInResponse(
            visit.Id, container.ContainerNumber, block.Code,
            visit.Bay, visit.Row, visit.Tier, visit.GateInAt));
    }

    // Open visit = not gated out yet
    private Task<bool> IsInYardAsync(Guid containerId, CancellationToken cancellationToken) =>
        dbContext.ContainerVisits.AnyAsync(v => v.ContainerId == containerId && v.GateOutAt == null, cancellationToken);
}
