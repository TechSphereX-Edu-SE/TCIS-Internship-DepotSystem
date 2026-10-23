using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;

public sealed class GetAllLineOperatorsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllLineOperatorsQuery, Result<PagedResult<LineOperatorResponse>>>
{
    public async Task<Result<PagedResult<LineOperatorResponse>>> HandleAsync(
        GetAllLineOperatorsQuery query, CancellationToken cancellationToken = default)
    {
        var lineOperators = dbContext.LineOperators.AsNoTracking();
        IOrderedQueryable<LineOperator> ordered;

        lineOperators = query.Role switch
        {
            PartnerRole.Owner => lineOperators.Where(l => l.IsOwner),
            PartnerRole.Operator => lineOperators.Where(l => l.IsOperator),
            _ => lineOperators
        };

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            // Master data list: sorted by code for easy lookup
            ordered = lineOperators.OrderBy(l => l.Code);
        }
        else
        {
            // Codes are stored upper case; names are compared lower case on both sides
            var code = query.Search.Trim().ToUpperInvariant();
            var name = query.Search.Trim().ToLower();
            lineOperators = lineOperators.Where(l => l.Code.Contains(code) || l.Name.ToLower().Contains(name));

            // Feedback 1: exact code first, then codes starting with the text, then name matches
            ordered = lineOperators
                .OrderBy(l => l.Code == code ? 0 : l.Code.StartsWith(code) ? 1 : 2)
                .ThenBy(l => l.Code);
        }

        var totalCount = await lineOperators.CountAsync(cancellationToken);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page.Select(LineOperatorResponse.From).ToList();
        return Result.Success(new PagedResult<LineOperatorResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
