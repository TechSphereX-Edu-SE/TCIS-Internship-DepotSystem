using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.Customers.GetAll;

public sealed class GetAllCustomersQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllCustomersQuery, Result<PagedResult<CustomerResponse>>>
{
    public async Task<Result<PagedResult<CustomerResponse>>> HandleAsync(
        GetAllCustomersQuery query, CancellationToken cancellationToken = default)
    {
        var customers = dbContext.Customers.AsNoTracking();
        IOrderedQueryable<Customer> ordered;

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            ordered = customers.OrderBy(c => c.Name).ThenBy(c => c.TaxCode);
        }
        else
        {
            // Customer lists grow over time, so allow searching by tax code or name
            var search = query.Search.Trim().ToLower();
            customers = customers.Where(c => c.TaxCode.Contains(search) || c.Name.ToLower().Contains(search));

            // Feedback 1: exact tax code first, then tax codes starting with the text, then name matches
            ordered = customers
                .OrderBy(c => c.TaxCode == search ? 0 : c.TaxCode.StartsWith(search) ? 1 : 2)
                .ThenBy(c => c.Name)
                .ThenBy(c => c.TaxCode);
        }

        var totalCount = await customers.CountAsync(cancellationToken);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(CustomerResponse.From).ToList();
        return Result.Success(new PagedResult<CustomerResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
