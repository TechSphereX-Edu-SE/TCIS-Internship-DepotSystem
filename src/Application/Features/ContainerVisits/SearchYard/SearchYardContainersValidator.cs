using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

public sealed class SearchYardContainersValidator : AbstractValidator<SearchYardContainersQuery>
{
    public SearchYardContainersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Sort).IsInEnum();
        // Same filter rules as the yard inventory report
        Include(new YardVisitFilterValidator());
    }
}
