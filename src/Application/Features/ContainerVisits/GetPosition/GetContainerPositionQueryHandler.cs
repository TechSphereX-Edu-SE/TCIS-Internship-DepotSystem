using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;

/*
    Current position = the open visit (not gated out) of the container.
    404 Container.NotFound when the number is unknown, 404 ContainerVisit.NotInYard when the
    container exists but is not in the yard now.
*/
public sealed class GetContainerPositionQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetContainerPositionQuery, Result<ContainerPositionResponse>>
{
    public async Task<Result<ContainerPositionResponse>> HandleAsync(
        GetContainerPositionQuery query, CancellationToken cancellationToken = default)
    {
        // Same normalization as when the container was created (trim, upper case)
        var number = ContainerNumberRules.Normalize(query.ContainerNumber);

        var containerId = await dbContext.Containers
            .AsNoTracking()
            .Where(c => c.ContainerNumber == number)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (containerId is null)
            return Result.Failure<ContainerPositionResponse>(Error.NotFound("Container.NotFound",
                $"Container '{number}' was not found."));

        var position = await dbContext.ContainerVisits
            .AsNoTracking()
            .Where(v => v.ContainerId == containerId && v.GateOutAt == null)
            .Select(YardProjections.ToPosition(dbContext))
            .FirstOrDefaultAsync(cancellationToken);

        return position is null
            ? Result.Failure<ContainerPositionResponse>(Error.NotFound("ContainerVisit.NotInYard",
                $"Container '{number}' is not in the yard."))
            : Result.Success(position);
    }
}
