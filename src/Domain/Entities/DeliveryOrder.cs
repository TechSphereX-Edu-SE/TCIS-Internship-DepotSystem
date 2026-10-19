using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

/// <summary>
/// Lệnh giao container rỗng của hãng khai thác (mục 3 tài liệu Depot).
/// Depot chỉ được giao container ra khi có lệnh còn hạn và còn số lượng.
/// </summary>
public sealed class DeliveryOrder : AuditableEntity
{
    private readonly List<DeliveryOrderLine> _lines = [];

    public string OrderNumber { get; private set; } = default!;
    public Guid LineOperatorId { get; private set; }
    public LineOperator LineOperator { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = default!;

    /// <summary>Hạn lệnh: ngày cuối cùng được phép giao container ra.</summary>
    public DateOnly ExpiryDate { get; private set; }

    /// <summary>Chuyến tàu xuất container khỏi Việt Nam.</summary>
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

    /// <summary>Thêm số lượng cho một loại container. Chỉ nên gọi khi tạo lệnh mới.</summary>
    public Result AddLine(ContainerSize size, ContainerCategory category, int quantity)
    {
        if (quantity < 1)
            return Result.Failure(Error.Validation("DeliveryOrder.InvalidQuantity", "Quantity must be at least 1."));

        if (_lines.Any(l => l.Size == size && l.Category == category))
            return Result.Failure(Error.Conflict("DeliveryOrder.DuplicateLine",
                $"Order already has a line for {size} {category}."));

        _lines.Add(new DeliveryOrderLine(Id, size, category, quantity));
        return Result.Success();
    }

    public bool IsExpired(DateOnly today) => today > ExpiryDate;

    /// <summary>Giữ một suất giao cho container có kích thước/loại tương ứng.</summary>
    internal Result Release(ContainerSize size, ContainerCategory category, DateOnly today)
    {
        if (IsExpired(today))
            return Result.Failure(Error.Conflict("DeliveryOrder.Expired",
                $"Delivery order {OrderNumber} expired on {ExpiryDate:dd/MM/yyyy}."));

        var line = _lines.FirstOrDefault(l => l.Size == size && l.Category == category);
        if (line is null)
            return Result.Failure(Error.Conflict("DeliveryOrder.TypeNotAllowed",
                $"Delivery order {OrderNumber} does not include {size} {category} containers."));

        return line.Release();
    }
}
