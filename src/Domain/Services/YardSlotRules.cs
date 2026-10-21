using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Services;

/*
    Occupancy rules for a physical block (spec section 1).
    Spec: odd bays hold 20ft, even bays hold 40ft, "2 odd bays = 1 even bay".
    So every position is measured in odd-bay units (its footprint):
      - 20ft in odd bay o   -> covers bay o
      - 40ft in even bay b  -> covers bays b-1 and b+1
    Two containers in the same row and tier clash when their footprints share a bay.
    This gives: 40ft at b blocks 20ft at b-1/b+1 (and the reverse), and also
    40ft at b-2/b+2 (they share bay b-1/b+1).

    Stacking (spec: tiers stack "from 2 to many"): a container above tier 1 must stand
    on containers. Inference (the spec does not detail it): every bay of its footprint
    must be covered at the tier just below, in the same row.
    E.g. a 40ft can sit on one 40ft or on two 20ft side by side, but not on a single 20ft.
*/
public static class YardSlotRules
{
    // Odd bays covered by a container placed in the given bay
    public static IReadOnlyList<int> Footprint(int bay, ContainerSize size) =>
        size == ContainerSize.Ft40 ? [bay - 1, bay + 1] : [bay];

    // A container already in the yard, in the same block and row as the new position
    public sealed record PlacedContainer(int Bay, int Tier, ContainerSize Size);

    // True when another container in the same tier shares any bay of the footprint
    public static bool Overlaps(int bay, int tier, ContainerSize size, IEnumerable<PlacedContainer> sameRow)
    {
        var footprint = Footprint(bay, size);
        return sameRow
            .Where(p => p.Tier == tier)
            .Any(p => Footprint(p.Bay, p.Size).Intersect(footprint).Any());
    }

    // True when the position is on the ground or every bay of the footprint is covered just below
    public static bool IsSupported(int bay, int tier, ContainerSize size, IEnumerable<PlacedContainer> sameRow)
    {
        if (tier == 1)
            return true;

        var coveredBelow = sameRow
            .Where(p => p.Tier == tier - 1)
            .SelectMany(p => Footprint(p.Bay, p.Size))
            .ToHashSet();

        return Footprint(bay, size).All(coveredBelow.Contains);
    }
}
