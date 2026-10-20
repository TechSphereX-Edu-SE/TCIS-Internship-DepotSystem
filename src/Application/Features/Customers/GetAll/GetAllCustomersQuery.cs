using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.GetAll;

// Optional search by tax code or name
public sealed record GetAllCustomersQuery(string? Search = null, int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<CustomerResponse>>>;
