using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

// One item of an estimate version (damage, repair method, amount, who pays and how far that is settled)
public sealed record RepairEstimateLineResponse(
    Guid Id,
    int Position,
    Guid? FindingId,
    string DamageDescription,
    string RepairMethod,
    decimal Amount,
    CostPayer Payer,
    string? PayerName,
    CostPayerStatus PayerStatus,
    string? PayerNote);

// Sum of the items of one version per paying party (allocation: who pays how much)
public sealed record PayerTotal(CostPayer Payer, string? PayerName, decimal Amount);

/*
    One version with its items. The latest approved version is the cost in force: a later version starts
    as a copy of the previous one, so it already holds the earlier items (inference, see RepairEstimate).
*/
public sealed record RepairEstimateResponse(
    Guid Id,
    int Version,
    RepairEstimateStatus Status,
    string Currency,
    decimal Total,
    string? Note,
    string? Evidence,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? SubmittedAt,
    string? SubmittedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionRecordedBy,
    string? ApproverParty,
    string? ApproverName,
    string? ApprovalReference,
    string? DecisionNote,
    IReadOnlyList<RepairEstimateLineResponse> Lines,
    IReadOnlyList<PayerTotal> PayerTotals);

// Item sent by the screen when it saves a draft (the whole table at once)
public sealed record RepairEstimateLineInput(
    Guid? FindingId,
    string DamageDescription,
    string RepairMethod,
    decimal Amount,
    CostPayer Payer,
    string? PayerName);

public static class RepairEstimateMapping
{
    // Built in memory from a loaded version (Lines included), so totals use the same rule as the domain
    public static RepairEstimateResponse ToResponse(this RepairEstimate estimate)
    {
        var lines = estimate.Lines.OrderBy(l => l.Position)
            .Select(l => new RepairEstimateLineResponse(l.Id, l.Position, l.FindingId, l.DamageDescription, l.RepairMethod,
                l.Amount, l.Payer, l.PayerName, l.PayerStatus, l.PayerNote))
            .ToList();

        // Parties in the order they first appear in the table
        var payerTotals = lines
            .GroupBy(l => (l.Payer, l.PayerName))
            .Select(g => new PayerTotal(g.Key.Payer, g.Key.PayerName, g.Sum(l => l.Amount)))
            .ToList();

        return new RepairEstimateResponse(
            estimate.Id, estimate.Version, estimate.Status, estimate.Currency, estimate.Total, estimate.Note, estimate.Evidence,
            estimate.CreatedAt, estimate.CreatedBy, estimate.SubmittedAt, estimate.SubmittedBy,
            estimate.DecidedAt, estimate.DecisionRecordedBy, estimate.ApproverParty, estimate.ApproverName,
            estimate.ApprovalReference, estimate.DecisionNote, lines, payerTotals);
    }

    // Short text for the case history (event notes are at most 500 characters)
    public static string HistoryText(this RepairEstimate estimate, string? detail = null)
    {
        var text = $"v{estimate.Version} · {estimate.Total:0.##} {estimate.Currency}";
        if (!string.IsNullOrWhiteSpace(detail))
            text += $" · {detail.Trim()}";
        return text.Length <= 500 ? text : text[..497] + "...";
    }
}
