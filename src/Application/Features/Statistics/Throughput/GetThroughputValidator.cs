using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;

public sealed class GetThroughputValidator : AbstractValidator<GetThroughputQuery>
{
    // Same rules as the detail rows (ThroughputFilterValidator)
    public GetThroughputValidator() => Include(new ThroughputFilterValidator());
}
