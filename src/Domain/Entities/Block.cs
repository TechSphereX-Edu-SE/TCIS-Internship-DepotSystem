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

    // Spec section 1: a row can stack "from 2 to many tiers"
    public const int MinTiers = 2;

    private Block() { }

    public static Block CreatePhysical(string code, int maxBays, int maxRows, int maxTiers, string? description = null) =>
        new()
        {
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsVirtual = false,
            MaxBays = maxBays,
            MaxRows = maxRows,
            MaxTiers = maxTiers
        };

    public static Block CreateVirtual(string code, string? description = null) =>
        new()
        {
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsVirtual = true
        };

    /* 
        Expands or shrinks the block. Checking for containers outside the new size
        belongs to the Application layer because it needs a database query.
    */
    public Result Resize(int maxBays, int maxRows, int maxTiers)
    {
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
