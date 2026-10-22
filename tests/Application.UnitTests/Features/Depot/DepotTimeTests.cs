using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class DepotTimeTests
{
    [Fact]
    public void DateOf_Should_Use_Vietnam_Date_For_Utc_Instants()
    {
        // 17:00 UTC = 00:00 next day in Vietnam
        DepotTime.DateOf(new DateTimeOffset(2026, 10, 20, 16, 59, 59, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 10, 20));
        DepotTime.DateOf(new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void StartOf_Should_Be_Midnight_Vietnam_In_Utc()
    {
        var start = DepotTime.StartOf(new DateOnly(2026, 10, 21));

        start.Should().Be(new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero));
        start.Offset.Should().Be(TimeSpan.Zero); // PostgreSQL timestamptz only accepts UTC values
    }

    [Fact]
    public void DwellDays_Should_Count_Vietnam_Calendar_Days()
    {
        var vn = TimeSpan.FromHours(7);
        var gateIn = new DateTimeOffset(2026, 10, 20, 23, 0, 0, vn);

        DepotTime.DwellDays(gateIn, new DateTimeOffset(2026, 10, 20, 23, 59, 0, vn)).Should().Be(0); // same day
        DepotTime.DwellDays(gateIn, new DateTimeOffset(2026, 10, 21, 0, 30, 0, vn)).Should().Be(1);  // next day, 90 minutes later
        DepotTime.DwellDays(gateIn, new DateTimeOffset(2026, 10, 30, 17, 0, 0, TimeSpan.Zero)).Should().Be(11); // 31/10 00:00 VN
    }

    [Fact]
    public void DateOf_Should_Not_Depend_On_The_Input_Offset()
    {
        var vietnam = new DateTimeOffset(2026, 10, 21, 6, 30, 0, TimeSpan.FromHours(7));
        var sameInstantInUtc = vietnam.ToUniversalTime(); // 20/10 23:30 UTC

        DepotTime.DateOf(vietnam).Should().Be(new DateOnly(2026, 10, 21));
        DepotTime.DateOf(sameInstantInUtc).Should().Be(new DateOnly(2026, 10, 21));
    }
}
