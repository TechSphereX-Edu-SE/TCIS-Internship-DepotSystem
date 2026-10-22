using TechSpherex.CleanArchitecture.Application.Abstractions.Messaging;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;

/*
    Containers in the yard now, per line operator and dwell time.
    Dwell time is split at LongStayDays (default 10): short stay = fewer days, long stay = that many or more.
    The split point is a parameter because the plan leaves the exact day ranges to confirm.
*/
public sealed record GetYardInventoryQuery(int LongStayDays = GetYardInventoryQuery.DefaultLongStayDays)
    : IQuery<Result<YardInventoryResponse>>
{
    public const int DefaultLongStayDays = 10;
}

public sealed record YardInventoryResponse(
    DateOnly AsOf,
    int LongStayDays,
    List<LineOperatorInventory> LineOperators,
    int TotalShortStay,
    int TotalLongStay,
    int Total);

// ShortStay: dwell days below LongStayDays; LongStay: LongStayDays or more
public sealed record LineOperatorInventory(
    Guid LineOperatorId,
    string Code,
    string Name,
    int ShortStay,
    int LongStay,
    int Total);
