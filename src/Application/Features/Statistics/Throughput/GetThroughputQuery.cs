using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

/*
    Gate-in and gate-out volume per line operator per day, between two Vietnam dates (inclusive).
    LineOperatorId is optional: without it every line operator is included.
*/
public sealed record GetThroughputQuery(DateOnly From, DateOnly To, Guid? LineOperatorId = null)
    : IQuery<Result<ThroughputResponse>>
{
    // Longest range in one request, to keep the result readable and the query bounded
    public const int MaxDays = 366;
}

public sealed record ThroughputResponse(
    DateOnly From,
    DateOnly To,
    List<DailyThroughput> Days,
    int TotalGateIn,
    int TotalGateOut);

// One row per day and line operator that had at least one gate-in or gate-out
public sealed record DailyThroughput(
    DateOnly Date,
    Guid LineOperatorId,
    string LineOperatorCode,
    int GateIn,
    int GateOut);
