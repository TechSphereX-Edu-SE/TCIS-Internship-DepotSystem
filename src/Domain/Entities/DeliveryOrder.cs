using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// Empty-container delivery order issued by a line operator (section 3 of the depot spec).
/// The depot may only release a container when a valid, unexpired order with remaining quantity exists.
/// </summary>
public sealed class DeliveryOrder : AuditableEntity
{
    private readonly List<DeliveryOrderLine> _lines = [];

    public string OrderNumber { get; private set; } = default!;
    public Guid LineOperatorId { get; private set; }
    public LineOperator LineOperator { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = default!;

    /// <summary>Last day on which containers may be released under this order.</summary>
    public DateOnly ExpiryDate { get; private set; }

    /// <summary>Vessel/voyage carrying the containers out of Vietnam.</summary>
    public string VesselVoyage { get; private set; } = default!;

    public IReadOnlyCollection<DeliveryOrderLine> Lines => _lines.AsReadOnly();

    private DeliveryOrder() { }

    public DeliveryOrder(string orderNumber, Guid lineOperatorId, Guid customerId, DateOnly expiryDate, string vesselVoyage)
    {
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        LineOperatorId = lineOperatorId;
        CustomerId = customerId;
        ExpiryDate = expiryDate;
        VesselVoyage = vesselVoyage.Trim().ToUpperInvariant();
    }

    /// <summary>Adds the quantity for one container category ("so luong container moi loai").</summary>
    public Result AddLine(ContainerCategory category, int quantity)
    {
        if (quantity < 1)
            return Result.Failure(Error.Validation("DeliveryOrder.InvalidQuantity", "Quantity must be at least 1."));

        if (_lines.Any(l => l.Category == category))
            return Result.Failure(Error.Conflict("DeliveryOrder.DuplicateLine",
                $"Order already has a line for {category} containers."));

        _lines.Add(new DeliveryOrderLine(Id, category, quantity));
        return Result.Success();
    }

    public bool IsExpired(DateOnly today) => today > ExpiryDate;

    /// <summary>Consumes one unit of the order for a container of the given category.</summary>
    internal Result Release(ContainerCategory category, DateOnly today)
    {
        if (IsExpired(today))
            return Result.Failure(Error.Conflict("DeliveryOrder.Expired",
                $"Delivery order {OrderNumber} expired on {ExpiryDate:dd/MM/yyyy}."));

        var line = _lines.FirstOrDefault(l => l.Category == category);
        if (line is null)
            return Result.Failure(Error.Conflict("DeliveryOrder.TypeNotAllowed",
                $"Delivery order {OrderNumber} does not include {category} containers."));

        return line.Release();
    }
}
