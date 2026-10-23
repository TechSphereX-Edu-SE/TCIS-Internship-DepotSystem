using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetDaily;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class GetDailyVisitsQueryHandlerTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    // 22/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, Vn);

    /*
        MSCU1234566 in 22/10 08:00 VN
        MSCU5551234 in 21/10 23:30 VN, out 22/10 08:30 VN under DO-001 (Dry 2 -> 1 left)
        CMAU1234564 in 21/10 17:30 UTC = 22/10 00:30 VN (the UTC date would say 21/10)
    */
    private static async Task SeedAsync(AppDbContext dbContext)
    {
        var msc = new LineOperator("MSC", "Mediterranean Shipping Company");
        var customer = new Customer("0301234567", "Customer A");
        var wait = Block.CreateVirtual("WAIT");

        Container NewContainer(string number) =>
            new(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        var today = NewContainer("MSCU1234566");
        var leftToday = NewContainer("MSCU5551234");
        var afterMidnight = NewContainer("CMAU1234564");

        ContainerVisit GateIn(Container container, DateTimeOffset at) =>
            ContainerVisit.GateIn(container, msc.Id, wait, null, null, null, ContainerGrade.A,
                ContainerCondition.Normal, null, "51C-11111", at).Value!;

        var leftVisit = GateIn(leftToday, new DateTimeOffset(2026, 10, 21, 23, 30, 0, Vn));
        var order = new DeliveryOrder("DO-001", msc.Id, customer.Id, new DateOnly(2026, 10, 31), "MSC VESSEL 01");
        order.AddLine(ContainerCategory.Dry, 2);
        leftVisit.GateOut(order, ContainerCategory.Dry, "51c-99999", new DateTimeOffset(2026, 10, 22, 8, 30, 0, Vn));

        dbContext.AddRange(msc, customer, wait, today, leftToday, afterMidnight, order, leftVisit,
            GateIn(today, new DateTimeOffset(2026, 10, 22, 8, 0, 0, Vn)),
            GateIn(afterMidnight, new DateTimeOffset(2026, 10, 21, 17, 30, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Task<Result<DailyVisitsResponse>> GetAsync(AppDbContext dbContext, DateOnly? date = null) =>
        new GetDailyVisitsQueryHandler(dbContext, new FixedTimeProvider(Now)).HandleAsync(
            new GetDailyVisitsQuery(date), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_Default_To_Today_In_Vietnam()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await GetAsync(dbContext);

        result.Value!.Date.Should().Be(new DateOnly(2026, 10, 22));
        // Newest first: 08:00 VN before 00:30 VN
        result.Value.GateIns.Select(v => v.ContainerNumber).Should().Equal("MSCU1234566", "CMAU1234564");

        var gateOut = result.Value.GateOuts.Should().ContainSingle().Subject;
        gateOut.ContainerNumber.Should().Be("MSCU5551234");
        gateOut.DeliveryOrderNumber.Should().Be("DO-001");
        gateOut.OrderLineQuantity.Should().Be(2);
        gateOut.OrderLineRemaining.Should().Be(1);
        gateOut.VehicleNo.Should().Be("51C-99999");
    }

    [Fact]
    public async Task Should_Return_The_Requested_Date()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await GetAsync(dbContext, new DateOnly(2026, 10, 21));

        result.Value!.GateIns.Select(v => v.ContainerNumber).Should().Equal("MSCU5551234");
        result.Value.GateOuts.Should().BeEmpty();
    }
}
