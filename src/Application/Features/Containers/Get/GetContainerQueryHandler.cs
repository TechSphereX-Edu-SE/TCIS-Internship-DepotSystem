using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Get;

public sealed class GetContainerQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetContainerQuery, Result<ContainerResponse>>
{
    public async Task<Result<ContainerResponse>> HandleAsync(
        GetContainerQuery query, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);

        return container is null
            ? Result.Failure<ContainerResponse>(
                Error.NotFound("Container.NotFound", $"Container with ID '{query.Id}' was not found."))
            : Result.Success(ContainerResponse.From(container));
    }
}
