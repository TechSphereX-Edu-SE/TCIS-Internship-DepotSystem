using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// Khu bãi container (mục 1 tài liệu Depot).
/// Block thật quản lý theo Bay - Row - Tier; block ảo không có vị trí chi tiết.
/// </summary>
public sealed class Block : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsVirtual { get; private set; }
    public int? MaxBays { get; private set; }
    public int? MaxRows { get; private set; }
    public int? MaxTiers { get; private set; }

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

    /// <summary>
    /// Mở rộng hoặc thu hẹp block. Việc kiểm tra container đang nằm ngoài
    /// kích thước mới thuộc về tầng Application (cần truy vấn dữ liệu).
    /// </summary>
    public Result Resize(int maxBays, int maxRows, int maxTiers)
    {
        if (IsVirtual)
            return Result.Failure(Error.Validation("Block.VirtualCannotResize", "A virtual block has no bay/row/tier."));

        if (maxBays < 1 || maxRows < 1 || maxTiers < 1)
            return Result.Failure(Error.Validation("Block.InvalidSize", "Bays, rows and tiers must be at least 1."));

        MaxBays = maxBays;
        MaxRows = maxRows;
        MaxTiers = maxTiers;
        return Result.Success();
    }

    /// <summary>
    /// Kiểm tra một vị trí có hợp lệ với block và kích thước container không.
    /// Bay lẻ chứa container 20ft, bay chẵn chứa container 40ft.
    /// </summary>
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

        return Result.Success();
    }
}
