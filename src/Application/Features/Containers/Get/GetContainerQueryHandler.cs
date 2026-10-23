using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.Containers.Get;

public sealed class GetContainerQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetContainerQuery, Result<ContainerResponse>>
{
    public async Task<Result<ContainerResponse>> HandleAsync(
        GetContainerQuery query, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers
            .AsNoTracking()
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        if (container is null)
            return Result.Failure<ContainerResponse>(
                Error.NotFound("Container.NotFound", $"Container with ID '{query.Id}' was not found."));

        var today = DepotTime.DateOf(timeProvider.GetUtcNow());
        var active = await dbContext.FindActiveAsync(container.Id, today, cancellationToken);
        return Result.Success(ContainerResponse.From(container, active?.Operator));
    }
}
