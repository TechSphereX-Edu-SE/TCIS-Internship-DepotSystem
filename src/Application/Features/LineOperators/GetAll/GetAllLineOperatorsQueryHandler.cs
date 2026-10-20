using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;

public sealed class GetAllLineOperatorsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetAllLineOperatorsQuery, Result<PagedResult<LineOperatorResponse>>>
{
    public async Task<Result<PagedResult<LineOperatorResponse>>> HandleAsync(
        GetAllLineOperatorsQuery query, CancellationToken cancellationToken = default)
    {
        var totalCount = await dbContext.LineOperators.CountAsync(cancellationToken);

        // Master data list: sorted by code for easy lookup
        var lineOperators = await dbContext.LineOperators
            .AsNoTracking()
            .OrderBy(l => l.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = lineOperators.Select(LineOperatorResponse.From).ToList();
        return Result.Success(new PagedResult<LineOperatorResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
