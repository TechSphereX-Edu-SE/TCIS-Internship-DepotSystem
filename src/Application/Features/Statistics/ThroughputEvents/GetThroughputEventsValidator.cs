using FluentValidation;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.ThroughputEvents;

public sealed class GetThroughputEventsValidator : AbstractValidator<GetThroughputEventsQuery>
{
    public GetThroughputEventsValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        // Same date range and filter rules as the totals
        Include(new ThroughputFilterValidator());
    }
}
