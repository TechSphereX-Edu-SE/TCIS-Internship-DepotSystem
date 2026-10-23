using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class StatisticsHandlerTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    // 21/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, Vn);

    private sealed record Seed(LineOperator Msc, LineOperator Cma, LineOperator Hmm);

    /*
        Yard used by every test (all in a virtual block, so positions do not matter):
          MSC  #1 in 20/10 10:00 VN            -> 1 day,  in yard
          MSC  #2 in 11/10 08:00 VN            -> 10 days, in yard
          MSC  #3 in 11/10 17:30 UTC           -> 12/10 00:30 VN = 9 days, in yard (UTC date would say 10)
          MSC  #4 in 20/10 17:30 UTC           -> 21/10 00:30 VN, in yard
          CMA  #5 in 01/10 08:00 VN            -> 20 days, in yard
          CMA  #6 in 05/10 08:00 VN, out 20/10 15:00 VN
        HMM has nothing.
    */
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(
            new LineOperator("MSC", "Mediterranean Shipping Company"),
            new LineOperator("CMA", "CMA CGM"),
            new LineOperator("HMM", "Hyundai Merchant Marine"));
        var customer = new Customer("0301234567", "Customer A");
        var wait = Block.CreateVirtual("WAIT");
        dbContext.AddRange(seed.Msc, seed.Cma, seed.Hmm, customer, wait);

        string[] numbers = ["MSCU1234566", "HMMU0000016", "HMMU0000021", "HMMU0000037", "CMAU1234564", "CMAU100000X"];
        var containers = numbers
            .Select(n => new Container(n, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "OWNER"))
            .ToList();
        dbContext.AddRange(containers);

        ContainerVisit GateIn(int index, LineOperator lineOperator, DateTimeOffset at) =>
            ContainerVisit.GateIn(containers[index], lineOperator.Id, wait, null, null, null,
                ContainerCondition.Normal, null, "51C-11111", at).Value!;

        var cmaOut = GateIn(5, seed.Cma, new DateTimeOffset(2026, 10, 5, 8, 0, 0, Vn));
        var order = new DeliveryOrder("DO-001", seed.Cma.Id, customer.Id, new DateOnly(2026, 10, 31), "CMA VESSEL 01");
        order.AddLine(ContainerCategory.Dry, 1);
        cmaOut.GateOut(order, ContainerCategory.Dry, ContainerSize.Ft20, "51C-99999", new DateTimeOffset(2026, 10, 20, 15, 0, 0, Vn));

        dbContext.AddRange(
            GateIn(0, seed.Msc, new DateTimeOffset(2026, 10, 20, 10, 0, 0, Vn)),
            GateIn(1, seed.Msc, new DateTimeOffset(2026, 10, 11, 8, 0, 0, Vn)),
            GateIn(2, seed.Msc, new DateTimeOffset(2026, 10, 11, 17, 30, 0, TimeSpan.Zero)),
            GateIn(3, seed.Msc, new DateTimeOffset(2026, 10, 20, 17, 30, 0, TimeSpan.Zero)),
            GateIn(4, seed.Cma, new DateTimeOffset(2026, 10, 1, 8, 0, 0, Vn)),
            order,
            cmaOut);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    private static Task<Result<YardInventoryResponse>> InventoryAsync(AppDbContext dbContext, int longStayDays = 10) =>
        new GetYardInventoryQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetYardInventoryQuery(longStayDays), TestContext.Current.CancellationToken);

    private static Task<Result<ThroughputResponse>> ThroughputAsync(AppDbContext dbContext, GetThroughputQuery query) =>
        new GetThroughputQueryHandler(dbContext).HandleAsync(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Inventory_Should_Split_By_Dwell_Days_Per_Line_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await InventoryAsync(dbContext);

        var report = result.Value!;
        report.AsOf.Should().Be(new DateOnly(2026, 10, 21));
        report.LineOperators.Select(r => r.Code).Should().Equal("CMA", "HMM", "MSC"); // every operator, by code
        report.LineOperators.Single(r => r.Code == "MSC").Should().Match<LineOperatorInventory>(r =>
            r.ShortStay == 3 && r.LongStay == 1 && r.Total == 4); // #1, #3, #4 short; #2 long
        report.LineOperators.Single(r => r.Code == "CMA").Should().Match<LineOperatorInventory>(r =>
            r.ShortStay == 0 && r.LongStay == 1); // #6 has left the yard
        report.LineOperators.Single(r => r.Code == "HMM").Total.Should().Be(0);
        report.TotalShortStay.Should().Be(3);
        report.TotalLongStay.Should().Be(2);
        report.Total.Should().Be(5);
    }

    [Fact]
    public async Task Inventory_Should_Use_The_Given_Split_Point()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await InventoryAsync(dbContext, longStayDays: 2);

        // MSC: #1 (1 day) and #4 (0 days) short; #2 (10) and #3 (9) long
        result.Value!.LineOperators.Single(r => r.Code == "MSC").Should().Match<LineOperatorInventory>(r =>
            r.ShortStay == 2 && r.LongStay == 2);
        result.Value.LongStayDays.Should().Be(2);
    }

    [Fact]
    public async Task Throughput_Should_Count_By_Vietnam_Date_And_Line_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await ThroughputAsync(dbContext, new GetThroughputQuery(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21)));

        // 20/10: MSC #1 in, CMA #6 out. 21/10: MSC #4 in (20/10 17:30 UTC is 21/10 in Vietnam)
        result.Value!.Days.Select(d => (d.Date.Day, d.LineOperatorCode, d.GateIn, d.GateOut)).Should().Equal(
            (20, "CMA", 0, 1),
            (20, "MSC", 1, 0),
            (21, "MSC", 1, 0));
        result.Value.TotalGateIn.Should().Be(2);
        result.Value.TotalGateOut.Should().Be(1);
    }

    [Fact]
    public async Task Throughput_Should_Filter_By_Line_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await ThroughputAsync(dbContext,
            new GetThroughputQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), seed.Cma.Id));

        // CMA: #5 in 01/10, #6 in 05/10, #6 out 20/10
        result.Value!.Days.Should().OnlyContain(d => d.LineOperatorCode == "CMA");
        result.Value.TotalGateIn.Should().Be(2);
        result.Value.TotalGateOut.Should().Be(1);
    }

    [Fact]
    public async Task Throughput_Should_Return_NotFound_For_Unknown_Line_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var result = await ThroughputAsync(dbContext,
            new GetThroughputQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Guid.NewGuid()));

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validators_Should_Check_Range_And_Split_Point()
    {
        var throughput = new GetThroughputValidator();
        var october = new GetThroughputQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        throughput.Validate(october).IsValid.Should().BeTrue();
        throughput.Validate(october with { To = new DateOnly(2026, 9, 30) }).IsValid.Should().BeFalse();   // To before From
        throughput.Validate(october with { To = new DateOnly(2027, 10, 1) }).IsValid.Should().BeTrue();    // 366 days, the limit
        throughput.Validate(october with { To = new DateOnly(2027, 10, 2) }).IsValid.Should().BeFalse();   // 367 days

        var inventory = new GetYardInventoryValidator();
        inventory.Validate(new GetYardInventoryQuery(10)).IsValid.Should().BeTrue();
        inventory.Validate(new GetYardInventoryQuery(0)).IsValid.Should().BeFalse();
    }
}
