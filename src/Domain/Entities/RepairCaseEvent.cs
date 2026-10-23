using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;

namespace TechSpherex.CleanArchitecture.Domain.Entities;

// History row of a case: rows are only added, never changed (feedback 1: giữ lịch sử)
public sealed class RepairCaseEvent : BaseEntity
{
    public Guid RepairCaseId { get; private set; }
    public RepairEventKind Kind { get; private set; }
    public RepairCaseStatus? FromStatus { get; private set; }
    public RepairCaseStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? By { get; private set; }

    private RepairCaseEvent() { }

    internal static RepairCaseEvent Create(
        RepairCase repairCase, RepairEventKind kind, RepairCaseStatus? from, RepairCaseStatus to,
        string? note, DateTimeOffset at, string? actor) =>
        new()
        {
            RepairCaseId = repairCase.Id,
            Kind = kind,
            FromStatus = from,
            ToStatus = to,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            At = at,
            By = actor
        };
}
