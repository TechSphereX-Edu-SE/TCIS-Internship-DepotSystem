using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>Quantity of one container category within a delivery order.</summary>
public sealed class DeliveryOrderLine : BaseEntity
{
    public Guid DeliveryOrderId { get; private set; }
    public ContainerCategory Category { get; private set; }
    public int Quantity { get; private set; }
    public int ReleasedQuantity { get; private set; }

    public int RemainingQuantity => Quantity - ReleasedQuantity;

    private DeliveryOrderLine() { }

    internal DeliveryOrderLine(Guid deliveryOrderId, ContainerCategory category, int quantity)
    {
        DeliveryOrderId = deliveryOrderId;
        Category = category;
        Quantity = quantity;
    }

    internal Result Release()
    {
        if (RemainingQuantity <= 0)
            return Result.Failure(Error.Conflict("DeliveryOrder.QuantityExceeded",
                $"All {Quantity} {Category} containers of this order have been released."));

        ReleasedQuantity++;
        return Result.Success();
    }
}
