namespace TechSpherex.CleanArchitecture.Application.UnitTests;

// Clock frozen at a given instant, so tests can assert times and Vietnam dates
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
