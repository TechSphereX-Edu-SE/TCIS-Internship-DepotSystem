using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
using static TechSpherex.CleanArchitecture.Domain.Services.YardSlotRules;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class YardSlotRulesTests
{
    private static PlacedContainer Ft20(int bay, int tier = 1) => new(bay, tier, ContainerSize.Ft20);
    private static PlacedContainer Ft40(int bay, int tier = 1) => new(bay, tier, ContainerSize.Ft40);

    [Fact]
    public void Footprint_Should_Cover_Odd_Bays()
    {
        YardSlotRules.Footprint(5, ContainerSize.Ft20).Should().Equal(5);
        YardSlotRules.Footprint(4, ContainerSize.Ft40).Should().Equal(3, 5);
    }

    [Theory]
    [InlineData(3)] // 20ft at b-1
    [InlineData(5)] // 20ft at b+1
    public void Ft40_Should_Overlap_Ft20_On_Either_Side(int bay20)
    {
        YardSlotRules.Overlaps(4, 1, ContainerSize.Ft40, [Ft20(bay20)]).Should().BeTrue();
    }

    [Theory]
    [InlineData(2)] // 40ft at 2 covers 1 and 3
    [InlineData(4)] // 40ft at 4 covers 3 and 5
    public void Ft20_Should_Overlap_Ft40_Next_To_It(int bay40)
    {
        YardSlotRules.Overlaps(3, 1, ContainerSize.Ft20, [Ft40(bay40)]).Should().BeTrue();
    }

    [Fact]
    public void Ft40_Should_Overlap_Ft40_Two_Bays_Away()
    {
        // 40ft at 4 (3,5) and 40ft at 6 (5,7) share bay 5
        YardSlotRules.Overlaps(6, 1, ContainerSize.Ft40, [Ft40(4)]).Should().BeTrue();
        // 40ft at 4 (3,5) and 40ft at 8 (7,9) do not touch
        YardSlotRules.Overlaps(8, 1, ContainerSize.Ft40, [Ft40(4)]).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_Should_Ignore_Other_Tiers()
    {
        YardSlotRules.Overlaps(5, 2, ContainerSize.Ft20, [Ft20(5, tier: 1)]).Should().BeFalse();
    }

    [Fact]
    public void HasContainerAbove_Should_Check_The_Tier_Just_Above()
    {
        YardSlotRules.HasContainerAbove(5, 1, ContainerSize.Ft20, [Ft20(5, tier: 2)]).Should().BeTrue();
        YardSlotRules.HasContainerAbove(5, 1, ContainerSize.Ft20, [Ft40(4, tier: 2)]).Should().BeTrue();  // 40ft covers bay 5
        YardSlotRules.HasContainerAbove(4, 1, ContainerSize.Ft40, [Ft20(3, tier: 2)]).Should().BeTrue();  // 20ft on one end
        YardSlotRules.HasContainerAbove(5, 1, ContainerSize.Ft20, [Ft20(7, tier: 2)]).Should().BeFalse(); // other bay
        YardSlotRules.HasContainerAbove(5, 1, ContainerSize.Ft20, [Ft20(5, tier: 3)]).Should().BeFalse(); // not just above
    }

    [Fact]
    public void Tier_One_Should_Always_Be_Supported()
    {
        YardSlotRules.IsSupported(5, 1, ContainerSize.Ft20, []).Should().BeTrue();
    }

    [Fact]
    public void Upper_Tier_Should_Need_Container_Below()
    {
        YardSlotRules.IsSupported(5, 2, ContainerSize.Ft20, []).Should().BeFalse();
        YardSlotRules.IsSupported(5, 2, ContainerSize.Ft20, [Ft20(5)]).Should().BeTrue();
        YardSlotRules.IsSupported(5, 3, ContainerSize.Ft20, [Ft20(5, tier: 1)]).Should().BeFalse(); // gap at tier 2
    }

    [Fact]
    public void Ft40_Should_Need_Both_Bays_Covered_Below()
    {
        YardSlotRules.IsSupported(4, 2, ContainerSize.Ft40, [Ft40(4)]).Should().BeTrue();
        YardSlotRules.IsSupported(4, 2, ContainerSize.Ft40, [Ft20(3), Ft20(5)]).Should().BeTrue();
        YardSlotRules.IsSupported(4, 2, ContainerSize.Ft40, [Ft20(3)]).Should().BeFalse();
    }

    [Fact]
    public void Ft20_Should_Be_Supported_By_Ft40_Below()
    {
        YardSlotRules.IsSupported(3, 2, ContainerSize.Ft20, [Ft40(4)]).Should().BeTrue();
    }
}
