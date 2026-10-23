using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/* 
      Yard block (section 1 of the depot spec).
      A physical block is managed by Bay - Row - Tier; a virtual block has no detailed position.
*/
public sealed class Block : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsVirtual { get; private set; }
    public int? MaxBays { get; private set; }
    public int? MaxRows { get; private set; }
    public int? MaxTiers { get; private set; }

    /*
        Container types this block may hold (feedback 3: a block can take several types).
        This list - not the block's name or description ("khu khô/lạnh") - is what gate-in and move check.
    */
    public List<ContainerCategory> AllowedCategories { get; private set; } = [];

    /*
        Soft delete (feedback 9): a block with history is archived instead of deleted, so old visits
        still show its code. An archived block takes no new container (gate-in / move).
    */
    public DateTimeOffset? ArchivedAt { get; private set; }
    public bool IsArchived => ArchivedAt is not null;

    /*
        Bumped every time a container enters or leaves the block. Saving it updates the block row,
        so its row version (xmin) changes: two operations on the same block at the same time
        (two gate-ins in neighbouring 40ft bays, a gate-in during a resize or a delete) cannot both
        commit - the second one gets a concurrency conflict and is asked to retry.
    */
    public int PlacementVersion { get; private set; }

    // PostgreSQL xmin row version (concurrency token), mapped in BlockConfiguration
    public uint RowVersion { get; private set; }

    // Spec section 1: a row can stack "from 2 to many tiers"
    public const int MinTiers = 2;

    // Every container type of Appendix II, used when a caller does not restrict the block
    public static IReadOnlyList<ContainerCategory> AllCategories { get; } = Enum.GetValues<ContainerCategory>();

    private Block() { }

    // allowedCategories: null = every type (the API always sends an explicit, non-empty list)
    public static Block CreatePhysical(
        string code, int maxBays, int maxRows, int maxTiers, string? description = null,
        IEnumerable<ContainerCategory>? allowedCategories = null) =>
        new()
        {
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsVirtual = false,
            MaxBays = maxBays,
            MaxRows = maxRows,
            MaxTiers = maxTiers,
            AllowedCategories = Distinct(allowedCategories ?? AllCategories)
        };

    public static Block CreateVirtual(
        string code, string? description = null, IEnumerable<ContainerCategory>? allowedCategories = null) =>
        new()
        {
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsVirtual = true,
            AllowedCategories = Distinct(allowedCategories ?? AllCategories)
        };

    /*
        Code and description can change (feedback 9); the Id stays the same, so every visit keeps
        pointing to this block. The code is unique - checked by the handler (needs a query).
    */
    public Result UpdateDetails(string code, string? description)
    {
        if (IsArchived)
            return Result.Failure(ArchivedError());

        Code = code.Trim().ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return Result.Success();
    }

    // Archives the block; the handler checks first that no container is in it
    public Result Archive(DateTimeOffset at)
    {
        if (IsArchived)
            return Result.Failure(ArchivedError());

        ArchivedAt = at;
        return Result.Success();
    }

    // Called when a container enters or leaves this block (see PlacementVersion)
    public void RecordContainerChange() => PlacementVersion++;

    // Replaces the allowed types; a block must accept at least one type or nothing could ever enter it
    public Result SetAllowedCategories(IEnumerable<ContainerCategory> categories)
    {
        if (IsArchived)
            return Result.Failure(ArchivedError());

        var list = Distinct(categories);
        if (list.Count == 0)
            return Result.Failure(Error.Validation("Block.NoCategories", "A block must accept at least one container type."));

        AllowedCategories = list;
        return Result.Success();
    }

    // Gate-in and move refuse an archived block, or a container whose type this block does not take (409)
    public Result CheckAccepts(ContainerCategory category)
    {
        if (IsArchived)
            return Result.Failure(ArchivedError());

        return AllowedCategories.Contains(category)
            ? Result.Success()
            : Result.Failure(Error.Conflict("Block.CategoryNotAllowed",
                $"Block {Code} does not take {category} containers (allowed: {string.Join(", ", AllowedCategories)})."));
    }

    private Error ArchivedError() =>
        Error.Conflict("Block.Archived", $"Block {Code} is archived; it is kept only for history.");

    // Same order as the enum, no duplicates, so the stored list is stable and easy to compare
    private static List<ContainerCategory> Distinct(IEnumerable<ContainerCategory> categories) =>
        categories.Distinct().Order().ToList();

    /* 
        Expands or shrinks the block. Checking for containers outside the new size
        belongs to the Application layer because it needs a database query.
    */
    public Result Resize(int maxBays, int maxRows, int maxTiers)
    {
        if (IsArchived)
            return Result.Failure(ArchivedError());

        if (IsVirtual)
            return Result.Failure(Error.Validation("Block.VirtualCannotResize", "A virtual block has no bay/row/tier."));

        // Spec section 1: bays hold one or more rows; tiers stack "from 2 to many"
        if (maxBays < 1 || maxRows < 1 || maxTiers < MinTiers)
            return Result.Failure(Error.Validation("Block.InvalidSize",
                $"Bays and rows must be at least 1 and tiers at least {MinTiers}."));

        MaxBays = maxBays;
        MaxRows = maxRows;
        MaxTiers = maxTiers;
        return Result.Success();
    }

    /* 
        Checks whether a position is valid for this block and container size.
        Odd bays hold 20ft containers, even bays hold 40ft containers.
    */
    public Result ValidateSlot(int? bay, int? row, int? tier, ContainerSize size)
    {
        if (IsVirtual)
        {
            return bay is null && row is null && tier is null
                ? Result.Success()
                : Result.Failure(Error.Validation("Block.VirtualHasNoSlot",
                    $"Block {Code} is virtual; bay, row and tier must be empty."));
        }

        if (bay is null || row is null || tier is null)
            return Result.Failure(Error.Validation("Block.SlotRequired",
                $"Block {Code} requires bay, row and tier."));

        if (bay < 1 || bay > MaxBays || row < 1 || row > MaxRows || tier < 1 || tier > MaxTiers)
            return Result.Failure(Error.Validation("Block.SlotOutOfRange",
                $"Position {bay}-{row}-{tier} is outside block {Code} ({MaxBays} bays, {MaxRows} rows, {MaxTiers} tiers)."));

        var isOddBay = bay % 2 == 1;
        if (size == ContainerSize.Ft20 && !isOddBay)
            return Result.Failure(Error.Validation("Block.BayParity", "A 20ft container must be placed in an odd bay."));

        if (size == ContainerSize.Ft40 && isOddBay)
            return Result.Failure(Error.Validation("Block.BayParity", "A 40ft container must be placed in an even bay."));

        // "2 odd bays = 1 even bay": a 40ft container in even bay b covers odd bays b-1 and b+1
        if (size == ContainerSize.Ft40 && bay + 1 > MaxBays)
            return Result.Failure(Error.Validation("Block.BayOutOfRange",
                $"A 40ft container in bay {bay} also covers bay {bay + 1}, which is outside block {Code} ({MaxBays} bays)."));

        return Result.Success();
    }
}
