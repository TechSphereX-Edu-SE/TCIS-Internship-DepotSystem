using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>Số lượng container mỗi loại của một lệnh giao.</summary>
public sealed class DeliveryOrderLine : BaseEntity
{
    public Guid DeliveryOrderId { get; private set; }
    public ContainerSize Size { get; private set; }
    public ContainerCategory Category { get; private set; }
    public int Quantity { get; private set; }
    public int ReleasedQuantity { get; private set; }

    public int RemainingQuantity => Quantity - ReleasedQuantity;

    private DeliveryOrderLine() { }

    internal DeliveryOrderLine(Guid deliveryOrderId, ContainerSize size, ContainerCategory category, int quantity)
    {
        DeliveryOrderId = deliveryOrderId;
        Size = size;
        Category = category;
        Quantity = quantity;
    }

    internal Result Release()
    {
        if (RemainingQuantity <= 0)
            return Result.Failure(Error.Conflict("DeliveryOrder.QuantityExceeded",
                $"All {Quantity} {Size} {Category} containers of this order have been released."));

        ReleasedQuantity++;
        return Result.Success();
    }
}
