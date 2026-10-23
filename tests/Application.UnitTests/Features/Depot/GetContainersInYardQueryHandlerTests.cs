using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class GetContainersInYardQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.FromHours(7));

    private sealed record Seed(Block A, Block Wait);

    /*
        Yard used by every test:
          MSCU1234566 in A 1-1-1
          MSCU5551234 in A 3-2-1 (row 2)
          CMAU1234564 in WAIT (virtual block)
          CSQU3054383 was in A 5-1-1 but has gated out -> never listed
    */
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var msc = new LineOperator("MSC", "Mediterranean Shipping Company");
        var customer = new Customer("0301234567", "Customer A");
        var a = Block.CreatePhysical("A", 10, 4, 3);
        var wait = Block.CreateVirtual("WAIT");

        Container NewContainer(string number) =>
            new(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        var first = NewContainer("MSCU1234566");
        var second = NewContainer("MSCU5551234");
        var inWait = NewContainer("CMAU1234564");
        var gone = NewContainer("CSQU3054383");

        ContainerVisit GateIn(Container container, Block block, int? bay, int? row, int? tier) =>
            ContainerVisit.GateIn(container, msc.Id, block, bay, row, tier, ContainerCondition.Normal, null, "51C-11111", Now).Value!;

        var goneVisit = GateIn(gone, a, 5, 1, 1);
        var order = new DeliveryOrder("DO-001", msc.Id, customer.Id, new DateOnly(2026, 10, 31), "MSC VESSEL 01");
        order.AddLine(ContainerCategory.Dry, 1);
        goneVisit.GateOut(order, ContainerCategory.Dry, "51C-99999", Now.AddHours(1));

        dbContext.AddRange(msc, customer, a, wait, first, second, inWait, gone, order,
            GateIn(first, a, 1, 1, 1), GateIn(second, a, 3, 2, 1), GateIn(inWait, wait, null, null, null), goneVisit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seed(a, wait);
    }

    private static Task<Result<IReadOnlyList<YardContainerResponse>>> GetAsync(
        AppDbContext dbContext, Guid? blockId = null, int? row = null) =>
        new GetContainersInYardQueryHandler(dbContext).HandleAsync(
            new GetContainersInYardQuery(blockId, row), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_List_Open_Visits_Sorted_By_Block_Row_Bay()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await GetAsync(dbContext);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(c => c.ContainerNumber).Should().Equal("MSCU1234566", "MSCU5551234", "CMAU1234564");
    }

    [Fact]
    public async Task Should_Filter_By_Block_And_Row()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await GetAsync(dbContext, seed.A.Id, row: 1);

        var container = result.Value!.Should().ContainSingle().Subject;
        container.ContainerNumber.Should().Be("MSCU1234566");
        container.BlockCode.Should().Be("A");
        container.Bay.Should().Be(1);
        container.Tier.Should().Be(1);
        container.Size.Should().Be(ContainerSize.Ft20);
        container.LineOperatorCode.Should().Be("MSC");
    }

    [Fact]
    public async Task Should_Return_Virtual_Block_Without_Position()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await GetAsync(dbContext, seed.Wait.Id);

        var container = result.Value!.Should().ContainSingle().Subject;
        container.ContainerNumber.Should().Be("CMAU1234564");
        container.Bay.Should().BeNull();
        container.Row.Should().BeNull();
    }
}
