using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetForContainer;

public sealed class GetContainerAssignmentsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetContainerAssignmentsQuery, Result<IReadOnlyList<OperatingAssignmentResponse>>>
{
    public async Task<Result<IReadOnlyList<OperatingAssignmentResponse>>> HandleAsync(
        GetContainerAssignmentsQuery query, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Containers.AnyAsync(c => c.Id == query.ContainerId, cancellationToken))
            return Result.Failure<IReadOnlyList<OperatingAssignmentResponse>>(Error.NotFound("Container.NotFound",
                $"Container with ID '{query.ContainerId}' was not found."));

        // Latest period first; the recording time breaks ties
        var items = await dbContext.OperatingAssignments
            .AsNoTracking()
            .Where(a => a.ContainerId == query.ContainerId)
            .OrderByDescending(a => a.ValidFrom)
            .ThenByDescending(a => a.CreatedAt)
            .ToResponse(dbContext)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<OperatingAssignmentResponse>>(items);
    }
}
