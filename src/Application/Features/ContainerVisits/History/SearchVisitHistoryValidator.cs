using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;

public sealed class SearchVisitHistoryValidator : AbstractValidator<SearchVisitHistoryQuery>
{
    public SearchVisitHistoryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Event).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.State).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'To' must be on or after 'From'.");
    }
}
