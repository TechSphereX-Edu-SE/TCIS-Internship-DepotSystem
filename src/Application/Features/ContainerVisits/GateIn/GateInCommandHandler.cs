using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;

/*
    Brings a container into the yard.
    Order of checks:
      1. Container, line operator and block exist (404).
      2. The container is not already in the yard (409) - one open visit per container.
      3. The position fits the block and the container size (400) - done by the domain
         (ContainerVisit.GateIn -> Block.ValidateSlot), so the rule lives in one place.
      4. The position is free and supported (409) - YardSlotRules: no overlap with
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

        // The domain validates the slot (range, bay parity, 40ft footprint) before creating the visit.
        // Nothing is saved on failure, so the condition it sets on the tracked container is discarded.
        var gateIn = ContainerVisit.GateIn(
            container, command.LineOperatorId, block,
            command.Bay, command.Row, command.Tier,
            command.Grade, command.Condition, command.ConditionNote,
            command.VehicleNo, timeProvider.GetUtcNow());
        if (gateIn.IsFailure)
            return Result.Failure<GateInResponse>(gateIn.Error!);

        // A virtual block has no slots, so any number of containers can be in it
        if (!block.IsVirtual)
        {
            var placement = await CheckPlacementAsync(
                block, command.Bay!.Value, command.Row!.Value, command.Tier!.Value, container.Size, cancellationToken);
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

    /*
        Loads the containers in the yard that can affect this position - same block and row,
        at this tier (overlap) or the tier below (support) - then applies YardSlotRules.
        Only two tiers of one row are read, so the query stays small.
    */
    private async Task<Result> CheckPlacementAsync(
        Block block, int bay, int row, int tier, ContainerSize size, CancellationToken cancellationToken)
    {
        var sameRow = await dbContext.ContainerVisits
            .Where(v => v.BlockId == block.Id &&
                        v.GateOutAt == null &&
                        v.Row == row &&
                        (v.Tier == tier || v.Tier == tier - 1))
            .Select(v => new YardSlotRules.PlacedContainer(v.Bay!.Value, v.Tier!.Value, v.Container.Size))
            .ToListAsync(cancellationToken);

        if (YardSlotRules.Overlaps(bay, tier, size, sameRow))
            return Result.Failure(Error.Conflict("ContainerVisit.SlotOccupied",
                $"Position {bay}-{row}-{tier} in block {block.Code} overlaps a container already there " +
                "(a 40ft in an even bay also takes the odd bays on each side)."));

        if (!YardSlotRules.IsSupported(bay, tier, size, sameRow))
            return Result.Failure(Error.Conflict("ContainerVisit.NoSupportBelow",
                $"Position {bay}-{row}-{tier} in block {block.Code} has no container underneath at tier {tier - 1}."));

        return Result.Success();
    }
}
