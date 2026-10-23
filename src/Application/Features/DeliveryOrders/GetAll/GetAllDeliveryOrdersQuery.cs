using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;

/*
    Delivery order list (management page) and the order dropdown of gate-out.
      - Search: order number (exact, then starting with the text, then containing it) or customer name.
      - Status: one status of DeliveryOrderStatus.
      - Usable: only orders a container can still be released under (feedback 6: Chưa giao / Đang giao,
        not expired, quantity left); completed, cancelled and expired orders stay on the management page.
      - OperatorId: orders of one line operator.
    Every filter runs in SQL before paging, so the count matches the rows.
*/
public sealed record GetAllDeliveryOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    DeliveryOrderStatus? Status = null,
    bool Usable = false,
    Guid? OperatorId = null)
    : IQuery<Result<PagedResult<DeliveryOrderResponse>>>;
