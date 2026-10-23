using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetGateOutCandidates;

/*
    Feedback 6: the container dropdown of gate-out, after the delivery order was chosen.
    Lists the containers in the yard that match the order: same line operator as their visit (taken from
    the confirmed operating assignment at gate-in, never from the prefix) and the type + size of a line
    with quantity left. Containers that match but cannot leave yet are listed too, with the reason,
    so the clerk sees "not allowed yet" instead of "not found".
    Search: part of the container number (exact, then prefix, then the rest), as in every search box.
*/
public sealed record GetGateOutCandidatesQuery(Guid DeliveryOrderId, string? Search = null, int Page = 1, int PageSize = 20)
    : IQuery<Result<PagedResult<GateOutCandidate>>>;

/*
    Why a matching container cannot be released now (null = it can):
      RepairCase     - an open inspection / repair case (RepairCaseId, RepairStatus); clicking opens the case
      Damaged        - marked damaged without an open case (older data); the repair page opens one at start-up
      ContainerAbove - another container rests on it (AboveContainerNumber); not a repair matter
*/
public sealed record GateOutCandidate(
    Guid VisitId,
    Guid ContainerId,
    string ContainerNumber,
    string IsoCode,
    ContainerSize Size,
    ContainerCategory Category,
    string BlockCode,
    int? Bay,
    int? Row,
    int? Tier,
    ContainerCondition Condition,
    DateTimeOffset GateInAt,
    string? BlockReason,
    Guid? RepairCaseId,
    RepairCaseStatus? RepairStatus,
    string? AboveContainerNumber)
{
    public bool Eligible => BlockReason is null;
}
