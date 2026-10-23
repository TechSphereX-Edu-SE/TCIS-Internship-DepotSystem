using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;

public sealed class GetYardInventoryValidator : AbstractValidator<GetYardInventoryQuery>
{
    public GetYardInventoryValidator()
    {
        // At least 1 day so the short-stay group is never empty by definition; at most a year
        RuleFor(x => x.LongStayDays).InclusiveBetween(1, 365);
        // Same filter rules as the detail rows (yard search)
        Include(new YardVisitFilterValidator());
    }
}
