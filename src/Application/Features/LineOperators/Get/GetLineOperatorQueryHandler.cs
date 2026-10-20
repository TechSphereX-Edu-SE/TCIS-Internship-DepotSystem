using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;

public sealed class GetLineOperatorQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetLineOperatorQuery, Result<LineOperatorResponse>>
{
    public async Task<Result<LineOperatorResponse>> HandleAsync(
        GetLineOperatorQuery query, CancellationToken cancellationToken = default)
    {
        var lineOperator = await dbContext.LineOperators
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.Id, cancellationToken);

        return lineOperator is null
            ? Result.Failure<LineOperatorResponse>(
                Error.NotFound("LineOperator.NotFound", $"Line operator with ID '{query.Id}' was not found."))
            : Result.Success(LineOperatorResponse.From(lineOperator));
    }
}
