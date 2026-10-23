using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// Shared lookups of the estimate commands (tracked: the caller changes and saves)
public static class RepairEstimateLoader
{
    // The version with its items and its case; null when the id is unknown
    public static async Task<(RepairEstimate Estimate, RepairCase Case)?> FindEstimateAsync(
        this IAppDbContext dbContext, Guid estimateId, CancellationToken cancellationToken)
    {
        var estimate = await dbContext.RepairEstimates
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == estimateId, cancellationToken);
        if (estimate is null)
            return null;

        var repairCase = await dbContext.RepairCases.FirstAsync(c => c.Id == estimate.RepairCaseId, cancellationToken);
        return (estimate, repairCase);
    }

    public static Error NotFound(Guid id) =>
        Error.NotFound("RepairEstimate.NotFound", $"Repair estimate with ID '{id}' was not found.");
}
