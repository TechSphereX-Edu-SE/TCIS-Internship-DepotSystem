using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    Database side of YardSlotRules, shared by gate-in, move and (later) gate-out.
    Each check loads only the containers in the yard that can matter - same block and row,
    and the tiers next to the position - then applies the pure rules in YardSlotRules.
    Virtual blocks have no positions, so callers skip these checks for them.
*/
public static class YardPlacement
{
    /*
        The position must not overlap a container in the same tier and must stand on containers
        below. excludeVisitId leaves out the container being moved, so its current place
        neither blocks nor supports its new place.
    */
    public static async Task<Result> CheckPlacementAsync(
        this IAppDbContext dbContext,
        Block block, int bay, int row, int tier, ContainerSize size,
        Guid? excludeVisitId,
        CancellationToken cancellationToken)
    {
        var sameRow = await LoadSameRowAsync(dbContext, block.Id, row, tier - 1, tier, excludeVisitId, cancellationToken);

        if (YardSlotRules.Overlaps(bay, tier, size, sameRow))
            return Result.Failure(Error.Conflict("ContainerVisit.SlotOccupied",
                $"Position {bay}-{row}-{tier} in block {block.Code} overlaps a container already there " +
                "(a 40ft in an even bay also takes the odd bays on each side)."));

        if (!YardSlotRules.IsSupported(bay, tier, size, sameRow))
            return Result.Failure(Error.Conflict("ContainerVisit.NoSupportBelow",
                $"Position {bay}-{row}-{tier} in block {block.Code} has no container underneath at tier {tier - 1}."));

        return Result.Success();
    }

    // A container that another container rests on cannot leave its place (move or gate-out)
    public static async Task<Result> CheckNothingAboveAsync(
        this IAppDbContext dbContext, ContainerVisit visit, ContainerSize size, CancellationToken cancellationToken)
    {
        if (visit.Bay is not int bay || visit.Row is not int row || visit.Tier is not int tier)
            return Result.Success(); // virtual block: no stacking

        var above = await LoadSameRowAsync(dbContext, visit.BlockId, row, tier + 1, tier + 1, visit.Id, cancellationToken);

        return YardSlotRules.HasContainerAbove(bay, tier, size, above)
            ? Result.Failure(Error.Conflict("ContainerVisit.ContainerAbove",
                $"Another container is stacked on this one at tier {tier + 1}. Move that container first."))
            : Result.Success();
    }

    // Containers in the yard in one row of a block, between two tiers (inclusive)
    private static Task<List<YardSlotRules.PlacedContainer>> LoadSameRowAsync(
        IAppDbContext dbContext, Guid blockId, int row, int fromTier, int toTier,
        Guid? excludeVisitId, CancellationToken cancellationToken) =>
        dbContext.ContainerVisits
            .Where(v => v.BlockId == blockId &&
                        v.GateOutAt == null &&
                        v.Row == row &&
                        v.Tier >= fromTier && v.Tier <= toTier &&
                        v.Id != excludeVisitId)
            .Select(v => new YardSlotRules.PlacedContainer(v.Bay!.Value, v.Tier!.Value, v.Container.Size))
            .ToListAsync(cancellationToken);
}
