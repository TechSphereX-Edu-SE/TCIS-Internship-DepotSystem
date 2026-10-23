using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.OperatingAssignments.GetActive;

// 404 OperatingAssignment.NoneActive when nothing confirmed covers the date: gate-in must wait for verification
public sealed class GetActiveAssignmentQueryHandler(IAppDbContext dbContext, TimeProvider timeProvider)
    : IQueryHandler<GetActiveAssignmentQuery, Result<OperatingAssignmentResponse>>
{
    public async Task<Result<OperatingAssignmentResponse>> HandleAsync(
        GetActiveAssignmentQuery query, CancellationToken cancellationToken = default)
    {
        var date = query.Date ?? DepotTime.DateOf(timeProvider.GetUtcNow());

        var active = await dbContext.OperatingAssignments
            .AsNoTracking()
            .Where(a => a.ContainerId == query.ContainerId)
            .ActiveOn(date)
            .OrderByDescending(a => a.ValidFrom)
            .ToResponse(dbContext)
            .FirstOrDefaultAsync(cancellationToken);

        return active is null
            ? Result.Failure<OperatingAssignmentResponse>(Error.NotFound("OperatingAssignment.NoneActive",
                $"No confirmed operating assignment covers {date:dd/MM/yyyy} for this container."))
            : Result.Success(active);
    }
}
