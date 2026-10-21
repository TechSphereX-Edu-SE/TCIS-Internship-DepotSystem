using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;

public sealed record GetAllDeliveryOrdersQuery(int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<DeliveryOrderResponse>>>;
