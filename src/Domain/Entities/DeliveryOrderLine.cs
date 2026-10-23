using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/*
    Quantity of one container type and size within a delivery order (Q1 23/10/2026: lines by type + size).
    Size is null only on lines created before sizes were recorded: such a line takes both sizes.
*/
public sealed class DeliveryOrderLine : BaseEntity
{
    public Guid DeliveryOrderId { get; private set; }
    public ContainerCategory Category { get; private set; }
    public ContainerSize? Size { get; private set; }
    public int Quantity { get; private set; }
    public int ReleasedQuantity { get; private set; }

    public int RemainingQuantity => Quantity - ReleasedQuantity;

    private DeliveryOrderLine() { }

    internal DeliveryOrderLine(Guid deliveryOrderId, ContainerCategory category, ContainerSize? size, int quantity)
    {
        DeliveryOrderId = deliveryOrderId;
        Category = category;
        Size = size;
        Quantity = quantity;
    }

    // A container fits the line when the type is the same and the size is the same (or the line has no size)
    public bool Fits(ContainerCategory category, ContainerSize size) =>
        Category == category && (Size is null || Size == size);

    // Two lines of one order cannot both take the same container
    internal bool Overlaps(ContainerCategory category, ContainerSize? size) =>
        Category == category && (Size is null || size is null || Size == size);

    internal Result Release()
    {
        if (RemainingQuantity <= 0)
            return Result.Failure(Error.Conflict("DeliveryOrder.QuantityExceeded",
                $"All {Quantity} {Category} {Size} containers of this order have been released."));

        ReleasedQuantity++;
        return Result.Success();
    }
}
