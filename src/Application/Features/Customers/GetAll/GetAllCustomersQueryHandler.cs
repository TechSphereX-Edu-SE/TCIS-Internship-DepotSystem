using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.GetAll;

public sealed class GetAllCustomersQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllCustomersQuery, Result<PagedResult<CustomerResponse>>>
{
    public async Task<Result<PagedResult<CustomerResponse>>> HandleAsync(
        GetAllCustomersQuery query, CancellationToken cancellationToken = default)
    {
        var customers = dbContext.Customers.AsNoTracking();

        // Customer lists grow over time, so allow searching by tax code or name
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            customers = customers.Where(c => c.TaxCode.Contains(search) || c.Name.ToLower().Contains(search));
        }

        var totalCount = await customers.CountAsync(cancellationToken);

        var page = await customers
            .OrderBy(c => c.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(CustomerResponse.From).ToList();
        return Result.Success(new PagedResult<CustomerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
