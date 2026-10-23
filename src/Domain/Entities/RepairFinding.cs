using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

// One reported damage (phát hiện) of a case: what was seen, where it was reported, when and by whom
public sealed class RepairFinding : BaseEntity
{
    public Guid RepairCaseId { get; private set; }
    public string Description { get; private set; } = default!;
    public RepairFindingSource Source { get; private set; }
    public Guid? VisitId { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }
    public string? ReportedBy { get; private set; }

    private RepairFinding() { }

    internal static RepairFinding Create(
        RepairCase repairCase, string description, RepairFindingSource source, Guid? visitId,
        DateTimeOffset at, string? actor) =>
        new()
        {
            RepairCaseId = repairCase.Id,
            Description = description.Trim(),
            Source = source,
            VisitId = visitId,
            ReportedAt = at,
            ReportedBy = actor
        };
}
