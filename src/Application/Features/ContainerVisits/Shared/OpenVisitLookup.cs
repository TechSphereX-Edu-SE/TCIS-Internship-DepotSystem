using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;

/*
    Finds the open visit (not gated out) of a container, shared by move and gate-out.
    The container and line operator are loaded too, since both use them in checks and responses.
*/
public static class OpenVisitLookup
{
    public static Task<ContainerVisit?> FindOpenVisitAsync(
        this IAppDbContext dbContext, Guid containerId, CancellationToken cancellationToken) =>
        dbContext.ContainerVisits
            .Include(v => v.Container)
            .Include(v => v.LineOperator)
            // Loaded so a move or gate-out can mark the block it leaves (Block.RecordContainerChange)
            .Include(v => v.Block)
            .FirstOrDefaultAsync(v => v.ContainerId == containerId && v.GateOutAt == null, cancellationToken);

    // Tells apart "no such container" and "container exists but is not in the yard"
    public static async Task<Error> NotInYardErrorAsync(
        this IAppDbContext dbContext, Guid containerId, CancellationToken cancellationToken)
    {
        var containerExists = await dbContext.Containers.AnyAsync(c => c.Id == containerId, cancellationToken);
        return containerExists
            ? Error.NotFound("ContainerVisit.NotInYard", $"Container with ID '{containerId}' is not in the yard.")
            : Error.NotFound("Container.NotFound", $"Container with ID '{containerId}' was not found.");
    }
}
