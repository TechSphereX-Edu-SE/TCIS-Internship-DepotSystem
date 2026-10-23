namespace TechSpherex.CleanArchitecture.Domain.Enums;

/*
    Why a container left the depot (feedback 6):
    EmptyRelease = cấp rỗng under a delivery order (uses its quantity);
    ExternalRepair = chuyển sửa ngoài under a repair transfer (no order, the container stays damaged).
*/
public enum GateOutPurpose
{
    EmptyRelease = 0,
    ExternalRepair = 1
}

/*
    Delivery order status (feedback 6), worked out from the lines and dates, never stored:
    NotStarted = nothing released yet (Chưa giao), InProgress = partly released (Đang giao),
    Completed = every line fully released (Đã giao đủ), Expired = past its expiry date before being
    completed, Cancelled = cancelled before anything was released (Q7).
*/
public enum DeliveryOrderStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Expired = 3,
    Cancelled = 4
}
