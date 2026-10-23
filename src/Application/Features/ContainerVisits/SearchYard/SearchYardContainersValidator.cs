using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;

public sealed class SearchYardContainersValidator : AbstractValidator<SearchYardContainersQuery>
{
    public SearchYardContainersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Repair).IsInEnum();
        RuleFor(x => x.MinDwellDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxDwellDays).GreaterThanOrEqualTo(0);
        // "From 10 to 5 days" would always be empty: report it instead of answering nothing
        RuleFor(x => x.MaxDwellDays)
            .GreaterThanOrEqualTo(x => x.MinDwellDays!.Value)
            .When(x => x.MinDwellDays is not null && x.MaxDwellDays is not null);
    }
}
