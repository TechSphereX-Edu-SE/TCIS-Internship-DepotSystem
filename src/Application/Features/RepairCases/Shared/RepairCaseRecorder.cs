using Microsoft.EntityFrameworkCore;
using TechSpherex.CleanArchitecture.Application.Abstractions.Data;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Shared;

// Result of a damage report: the case it went into, and whether that case was opened by this report
public sealed record RecordedDamage(Guid CaseId, bool Created, Guid FindingId);

/*
    Shared by gate-in, the damage report and the condition endpoint (feedback 4):
    the open case of the container gets a new finding; without an open case a new one is opened.
    Only adds rows to the context; the caller saves (one transaction with its own changes).
*/
public static class RepairCaseRecorder
{
    public static Task<RepairCase?> FindOpenCaseAsync(
        this IAppDbContext dbContext, Guid containerId, CancellationToken cancellationToken) =>
        dbContext.RepairCases.FirstOrDefaultAsync(
            c => c.ContainerId == containerId && c.Status != RepairCaseStatus.Passed, cancellationToken);

    public static async Task<RecordedDamage> RecordDamageAsync(
        this IAppDbContext dbContext, Container container, Guid? visitId, string description,
        RepairFindingSource source, DateTimeOffset at, string? actor, CancellationToken cancellationToken)
    {
        var open = await dbContext.FindOpenCaseAsync(container.Id, cancellationToken);
        if (open is not null)
        {
            // An open case is never closed here, so AddFinding cannot fail
            var (finding, added) = open.AddFinding(description, source, visitId, at, actor).Value;
            dbContext.RepairFindings.Add(finding);
            dbContext.RepairCaseEvents.Add(added);
            return new RecordedDamage(open.Id, false, finding.Id);
        }

        var (repairCase, first, opened) = RepairCase.Open(container, visitId, description, source, at, actor);
        dbContext.RepairCases.Add(repairCase);
        dbContext.RepairFindings.Add(first);
        dbContext.RepairCaseEvents.Add(opened);
        return new RecordedDamage(repairCase.Id, true, first.Id);
    }
}
