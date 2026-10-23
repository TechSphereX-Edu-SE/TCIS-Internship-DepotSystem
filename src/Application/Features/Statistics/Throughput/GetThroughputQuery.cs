using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

/*
    Gate-in and gate-out volume per line operator per day, between two Vietnam dates (inclusive).
    Every filter is optional except the dates; see IThroughputFilter for how each one applies.
*/
public sealed record GetThroughputQuery(
    DateOnly From,
    DateOnly To,
    Guid? LineOperatorId = null,
    ContainerCategory? Category = null,
    ContainerSize? Size = null,
    GateOutPurpose? Purpose = null)
    : IQuery<Result<ThroughputResponse>>, IThroughputFilter
{
    public const int MaxDays = ThroughputFiltering.MaxDays;
}

/*
    TotalGateOut = TotalReleased (cấp rỗng) + TotalToRepair (chuyển sửa ngoài): a physical gate-out is not
    always a release to a customer (feedback 7).
    LineOperators: one row per line operator with at least one event, the totals the detail tabs open from.
*/
public sealed record ThroughputResponse(
    DateOnly From,
    DateOnly To,
    List<DailyThroughput> Days,
    int TotalGateIn,
    int TotalGateOut,
    int TotalReleased,
    int TotalToRepair,
    List<OperatorThroughput> LineOperators);

// One row per day and line operator that had at least one gate-in or gate-out
public sealed record DailyThroughput(
    DateOnly Date,
    Guid LineOperatorId,
    string LineOperatorCode,
    int GateIn,
    int GateOut);

// Totals of one line operator over the period; GateOut = Released + ToRepair
public sealed record OperatorThroughput(
    Guid LineOperatorId,
    string Code,
    string Name,
    int GateIn,
    int GateOut,
    int Released,
    int ToRepair);
