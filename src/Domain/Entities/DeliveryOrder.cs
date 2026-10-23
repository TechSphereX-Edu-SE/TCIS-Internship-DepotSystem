using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;

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

    // Q7: an order can be cancelled (with a reason) only while nothing has been released under it
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    /*
        Time of the last release. Every release writes the order row, so with the row version (xmin)
        two gate-outs under one order, or a gate-out and a cancel, cannot both succeed (feedback 6:
        no double release, no release beyond the quantity); the later one gets 409 Data.ConcurrentUpdate.
    */
    public DateTimeOffset? LastReleasedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<DeliveryOrderLine> Lines => _lines.AsReadOnly();

    public bool IsCancelled => CancelledAt is not null;

    private DeliveryOrder() { }

    public DeliveryOrder(string orderNumber, Guid lineOperatorId, Guid customerId, DateOnly expiryDate, string vesselVoyage)
    {
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        LineOperatorId = lineOperatorId;
        CustomerId = customerId;
        ExpiryDate = expiryDate;
        VesselVoyage = vesselVoyage.Trim().ToUpperInvariant();
    }

    /*
        Adds the quantity for one container type and size (Q1). size null = both sizes; only used for
        lines from before sizes were recorded, the API always sends a size.
        A line cannot overlap another one: the same type and size twice, or a no-size line next to a
        sized line of the same type, would make it unclear which line a container uses.
    */
    public Result AddLine(ContainerCategory category, int quantity, ContainerSize? size = null)
    {
        if (quantity < 1)
            return Result.Failure(Error.Validation("DeliveryOrder.InvalidQuantity", "Quantity must be at least 1."));

        if (_lines.Any(l => l.Overlaps(category, size)))
            return Result.Failure(Error.Conflict("DeliveryOrder.DuplicateLine",
                $"Order already has a line for {category} {size} containers."));

        _lines.Add(new DeliveryOrderLine(Id, category, size, quantity));
        return Result.Success();
    }

    public bool IsExpired(DateOnly today) => today > ExpiryDate;

    // Feedback 6: Chưa giao / Đang giao / Đã giao đủ, plus Expired and Cancelled; see DeliveryOrderStatus
    public DeliveryOrderStatus StatusOn(DateOnly today) => StatusOf(
        IsCancelled, _lines.Sum(l => l.ReleasedQuantity), _lines.Sum(l => l.RemainingQuantity), ExpiryDate, today);

    // Shared with the list query, which computes the sums in SQL
    public static DeliveryOrderStatus StatusOf(bool cancelled, int released, int remaining, DateOnly expiryDate, DateOnly today)
    {
        if (cancelled) return DeliveryOrderStatus.Cancelled;
        if (remaining <= 0) return DeliveryOrderStatus.Completed;
        if (today > expiryDate) return DeliveryOrderStatus.Expired;
        return released == 0 ? DeliveryOrderStatus.NotStarted : DeliveryOrderStatus.InProgress;
    }

    // The line a container of this type and size uses (lines never overlap, so at most one fits)
    public DeliveryOrderLine? LineFor(ContainerCategory category, ContainerSize size) =>
        _lines.FirstOrDefault(l => l.Fits(category, size));

    // Q7: only an order with nothing released can be cancelled; the reason is kept for the history
    public Result Cancel(string reason, DateTimeOffset at)
    {
        if (IsCancelled)
            return Result.Failure(Error.Conflict("DeliveryOrder.AlreadyCancelled",
                $"Delivery order {OrderNumber} is already cancelled."));

        if (_lines.Any(l => l.ReleasedQuantity > 0))
            return Result.Failure(Error.Conflict("DeliveryOrder.HasReleases",
                $"Delivery order {OrderNumber} has released containers and cannot be cancelled."));

        CancelledAt = at;
        CancelReason = reason.Trim();
        return Result.Success();
    }

    /// <summary>Consumes one unit of the order for a container of the given type and size.</summary>
    internal Result Release(ContainerCategory category, ContainerSize size, DateTimeOffset at)
    {
        var today = DepotTime.DateOf(at);

        if (IsCancelled)
            return Result.Failure(Error.Conflict("DeliveryOrder.Cancelled",
                $"Delivery order {OrderNumber} has been cancelled."));

        if (IsExpired(today))
            return Result.Failure(Error.Conflict("DeliveryOrder.Expired",
                $"Delivery order {OrderNumber} expired on {ExpiryDate:dd/MM/yyyy}."));

        var line = LineFor(category, size);
        if (line is null)
            return Result.Failure(Error.Conflict("DeliveryOrder.TypeNotAllowed",
                $"Delivery order {OrderNumber} does not include {category} {size} containers."));

        var released = line.Release();
        if (released.IsSuccess)
            LastReleasedAt = at;
        return released;
    }
}
