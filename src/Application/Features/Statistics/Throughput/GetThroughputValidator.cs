using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

public sealed class GetThroughputValidator : AbstractValidator<GetThroughputQuery>
{
    public GetThroughputValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("'To' must be on or after 'From'.");

        // Both dates are included, so the range has (To - From + 1) days
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber < GetThroughputQuery.MaxDays)
            .When(x => x.To >= x.From)
            .WithName("Range")
            .WithMessage($"The range can be at most {GetThroughputQuery.MaxDays} days.");
    }
}
