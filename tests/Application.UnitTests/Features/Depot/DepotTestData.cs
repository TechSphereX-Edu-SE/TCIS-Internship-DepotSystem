using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Shared setup for the depot tests that need partners with roles or operating assignments
internal static class DepotTestData
{
    // Fixed past start, open end: covers every clock the tests use (fixed 2026 dates and the system clock)
    public static readonly DateOnly LongAgo = new(2000, 1, 1);

    // A partner that owns and operates containers, like MSC, CMA or HMM
    public static LineOperator Partner(string code) => new(code, code, isOwner: true, isOperator: true);

    // Confirmed assignment, not saved: the caller adds it with its other seed rows
    public static OperatingAssignment Confirmed(
        Container container, LineOperator lineOperator, DateOnly? from = null, DateOnly? to = null)
    {
        var assignment = OperatingAssignment.Create(container, lineOperator, from ?? LongAgo, to, "TEST", null).Value!;
        assignment.Confirm(DateTimeOffset.UtcNow);
        return assignment;
    }

    /*
        Makes the line operator the confirmed operator of the container from LongAgo, so gate-in accepts it.
        Earlier assignments of the container to another operator are cancelled, which lets a test
        gate the same container in again for a different operator.
    */
    public static async Task AssignAsync(AppDbContext dbContext, Container container, LineOperator lineOperator)
    {
        var existing = await dbContext.OperatingAssignments
            .Where(a => a.ContainerId == container.Id && a.Status != OperatingAssignmentStatus.Cancelled)
            .ToListAsync(TestContext.Current.CancellationToken);
        if (existing.Any(a => a.OperatorId == lineOperator.Id))
            return;

        foreach (var other in existing)
            other.Cancel("Test: operator changed", DateTimeOffset.UtcNow);

        dbContext.OperatingAssignments.Add(Confirmed(container, lineOperator));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
