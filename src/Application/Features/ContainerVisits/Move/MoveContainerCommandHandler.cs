using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Identity;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;

/*
    Moves a container that is in the yard.
    Order of checks:
      1. The container exists and is in the yard (404), the target block exists (404),
         and it is still where the screen saw it (409 PositionChanged, when the screen sent it).
      2. The target is not the current position (400).
      3. Nothing is stacked on the container (409) - otherwise the one above would float.
      4. The target block takes this container type (409) - domain (Block.CheckAccepts), same as gate-in.
      5. The target fits the block and the container size (400) - domain (Block.ValidateSlot).
      6. The target is free and supported (409) - same rules as gate-in, ignoring the container
         itself so its current place neither blocks nor supports the new one.
    Only then the domain moves the visit (ContainerVisit.MoveTo) and it is saved together with a
    ContainerMove row (feedback 7: the visit history lists every move with its time and user).
*/
public sealed class MoveContainerCommandHandler(
    IAppDbContext dbContext, TimeProvider timeProvider, ICurrentUser? currentUser = null)
    : ICommandHandler<MoveContainerCommand, Result<ContainerPositionResponse>>
{
    public async Task<Result<ContainerPositionResponse>> HandleAsync(
        MoveContainerCommand command, CancellationToken cancellationToken = default)
    {
        var visit = await dbContext.FindOpenVisitAsync(command.ContainerId, cancellationToken);
        if (visit is null)
            return Result.Failure<ContainerPositionResponse>(
                await dbContext.NotInYardErrorAsync(command.ContainerId, cancellationToken));

        if (command.FromBlockId is not null &&
            (visit.BlockId != command.FromBlockId || visit.Bay != command.FromBay ||
             visit.Row != command.FromRow || visit.Tier != command.FromTier))
            return Result.Failure<ContainerPositionResponse>(Error.Conflict("ContainerVisit.PositionChanged",
                $"Container {visit.Container.ContainerNumber} was moved meanwhile; it is now in block " +
                $"{visit.Block.Code} {visit.Bay}-{visit.Row}-{visit.Tier}. Check the new position first."));

        var target = await dbContext.Blocks.FindAsync([command.BlockId], cancellationToken);
        if (target is null)
            return Result.Failure<ContainerPositionResponse>(Error.NotFound("Block.NotFound",
                $"Block with ID '{command.BlockId}' was not found."));

        if (visit.BlockId == target.Id && visit.Bay == command.Bay && visit.Row == command.Row && visit.Tier == command.Tier)
            return Result.Failure<ContainerPositionResponse>(Error.Validation("ContainerVisit.SamePosition",
                $"Container {visit.Container.ContainerNumber} is already at this position."));

        // Checked on the current position, before the visit is changed
        var leave = await dbContext.CheckNothingAboveAsync(visit, visit.Container.Size, cancellationToken);
        if (leave.IsFailure)
            return Result.Failure<ContainerPositionResponse>(leave.Error!);

        var accepts = target.CheckAccepts(visit.Container.Category);
        if (accepts.IsFailure)
            return Result.Failure<ContainerPositionResponse>(accepts.Error!);

        // Domain slot check first (range, bay parity, 40ft footprint), so the visit is only
        // changed once every check has passed
        var slot = target.ValidateSlot(command.Bay, command.Row, command.Tier, visit.Container.Size);
        if (slot.IsFailure)
            return Result.Failure<ContainerPositionResponse>(slot.Error!);

        if (!target.IsVirtual)
        {
            var placement = await dbContext.CheckPlacementAsync(
                target, command.Bay!.Value, command.Row!.Value, command.Tier!.Value, visit.Container.Size,
                excludeVisitId: visit.Id, cancellationToken);
            if (placement.IsFailure)
                return Result.Failure<ContainerPositionResponse>(placement.Error!);
        }

        // Kept before MoveTo changes the visit, for the history row
        var from = (visit.BlockId, visit.Bay, visit.Row, visit.Tier);

        var move = visit.MoveTo(target, command.Bay, command.Row, command.Tier, visit.Container.Size);
        if (move.IsFailure)
            return Result.Failure<ContainerPositionResponse>(move.Error!);

        // Same save as the visit: a move is either recorded with its history row or not at all
        dbContext.ContainerMoves.Add(ContainerMove.Record(
            visit.Id, from, (target.Id, command.Bay, command.Row, command.Tier),
            timeProvider.GetUtcNow(), currentUser?.Email));

        await dbContext.SaveChangesAsync(cancellationToken);

        // Same shape as the position lookup, with the repair marker (the move does not change the case)
        var moved = await dbContext.ContainerVisits.AsNoTracking()
            .Where(v => v.Id == visit.Id)
            .Select(YardProjections.ToPosition(dbContext))
            .FirstAsync(cancellationToken);
        return Result.Success(moved);
    }
}
