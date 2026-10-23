using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.History;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.Throughput;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.ThroughputEvents;
using TechSpherex.CleanArchitecture.Application.Features.Statistics.YardInventory;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Feedback 7 + 8: report filters shared by totals and rows, throughput detail, container history, move history
public sealed class HistoryAndReportFilterTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    // 21/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, Vn);
    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October15 = new(2026, 10, 15);

    private sealed record Seed(
        LineOperator Msc, LineOperator Cma, LineOperator Hmm,
        Container C1, Container C2, Container C3, Container C4, Container C5, Container C6,
        ContainerVisit C6First, ContainerVisit C6Second, ContainerVisit C5Visit);

    private static DateTimeOffset On(int month, int day) => new(2026, month, day, 10, 0, 0, Vn);

    /*
        All in the virtual block WAIT. HMM only owns containers (no operator role).
          C1 MSC dry 20  owner HMM at gate-in   in 01/10                 -> in yard, 20 days
          C2 MSC reefer 40                      in 15/10                 -> in yard, 6 days
          C3 MSC dry 20  owner set after gate-in in 20/10                -> in yard, 1 day
          C4 CMA dry 20                         in 05/10                 -> in yard, 16 days
          C5 CMA dry 20                         in 25/09, out 10/10 cấp rỗng (DO-001)
          C6 MSC dry 20  visit 1                in 12/10, out 14/10 sửa ngoài
                         visit 2                in 16/10                 -> in yard, 5 days
    */
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var msc = DepotTestData.Partner("MSC");
        var cma = DepotTestData.Partner("CMA");
        var hmm = new LineOperator("HMM", "Hyundai", isOwner: true, isOperator: false);
        var customer = new Customer("0301234567", "Customer A");
        var wait = Block.CreateVirtual("WAIT");
        dbContext.AddRange(msc, cma, hmm, customer, wait);

        Container Dry20(string number) => new(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "OWNER");
        var c1 = Dry20("MSCU1000001");
        var c2 = new Container("MSCU1000002", "45R1", ContainerSize.Ft40, ContainerCategory.Reefer, 34000, 4800, null, "OWNER");
        var c3 = Dry20("MSCU1000003");
        var c4 = Dry20("CMAU1000004");
        var c5 = Dry20("CMAU1000005");
        var c6 = Dry20("MSCU1000006");
        dbContext.AddRange(c1, c2, c3, c4, c5, c6);

        c1.SetOwner(hmm).IsSuccess.Should().BeTrue();

        ContainerVisit GateIn(Container container, LineOperator lineOperator, DateTimeOffset at) =>
            ContainerVisit.GateIn(container, lineOperator.Id, wait, null, null, null,
                ContainerCondition.Normal, null, "51C-11111", at).Value!;

        var c5Visit = GateIn(c5, cma, On(9, 25));
        var order = new DeliveryOrder("DO-001", cma.Id, customer.Id, new DateOnly(2026, 10, 31), "CMA VESSEL 01");
        order.AddLine(ContainerCategory.Dry, 1);
        c5Visit.GateOut(order, ContainerCategory.Dry, ContainerSize.Ft20, "51C-99999", On(10, 10)).IsSuccess.Should().BeTrue();

        var c6First = GateIn(c6, msc, On(10, 12));
        c6First.GateOutForRepair("51C-88888", On(10, 14)).IsSuccess.Should().BeTrue();
        var c6Second = GateIn(c6, msc, On(10, 16));

        var c3Visit = GateIn(c3, msc, On(10, 20));
        // Owner recorded after the gate-in: the visit keeps "no data" (feedback 7)
        c3.SetOwner(hmm).IsSuccess.Should().BeTrue();

        dbContext.AddRange(
            GateIn(c1, msc, On(10, 1)),
            GateIn(c2, msc, On(10, 15)),
            c3Visit,
            GateIn(c4, cma, On(10, 5)),
            order, c5Visit, c6First, c6Second);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seed(msc, cma, hmm, c1, c2, c3, c4, c5, c6, c6First, c6Second, c5Visit);
    }

    private static FixedTimeProvider Clock => new(Now);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<YardInventoryResponse> InventoryAsync(AppDbContext dbContext, GetYardInventoryQuery query)
    {
        var result = await new GetYardInventoryQueryHandler(dbContext, Clock).HandleAsync(query, Token);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    private static async Task<PagedResult<YardContainerResponse>> YardAsync(AppDbContext dbContext, SearchYardContainersQuery query)
    {
        var result = await new SearchYardContainersQueryHandler(dbContext, Clock).HandleAsync(query, Token);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    private static async Task<ThroughputResponse> ThroughputAsync(AppDbContext dbContext, GetThroughputQuery query)
    {
        var result = await new GetThroughputQueryHandler(dbContext).HandleAsync(query, Token);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    private static async Task<PagedResult<VisitHistoryRow>> EventsAsync(AppDbContext dbContext, GetThroughputEventsQuery query)
    {
        var result = await new GetThroughputEventsQueryHandler(dbContext, Clock).HandleAsync(query, Token);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    private static async Task<PagedResult<VisitHistoryRow>> HistoryAsync(AppDbContext dbContext, SearchVisitHistoryQuery query)
    {
        var result = await new SearchVisitHistoryQueryHandler(dbContext, Clock).HandleAsync(query, Token);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task Inventory_Totals_Should_Match_Detail_Rows_With_The_Same_Filters()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var dry = await InventoryAsync(dbContext, new GetYardInventoryQuery(Category: ContainerCategory.Dry));
        var mscDry = await YardAsync(dbContext, new SearchYardContainersQuery(
            Category: ContainerCategory.Dry, OperatorId: seed.Msc.Id, Sort: YardSort.Oldest));
        var mscDryNewest = await YardAsync(dbContext, new SearchYardContainersQuery(
            Category: ContainerCategory.Dry, OperatorId: seed.Msc.Id, Sort: YardSort.Newest));

        // An owner-only partner is not a line operator row; CMA and MSC are
        dry.LineOperators.Select(r => (r.Code, r.ShortStay, r.LongStay, r.Total)).Should().Equal(
            ("CMA", 0, 1, 1),
            ("MSC", 2, 1, 3));
        dry.Total.Should().Be(4);
        dry.AsOfTime.Should().Be(Now);

        // The MSC row and its detail rows count the same containers; longest stay first by default
        mscDry.TotalCount.Should().Be(dry.LineOperators.Single(r => r.Code == "MSC").Total);
        mscDry.Items.Select(i => i.ContainerNumber).Should().Equal("MSCU1000001", "MSCU1000006", "MSCU1000003");
        mscDryNewest.Items.Select(i => i.ContainerNumber).Should().Equal("MSCU1000003", "MSCU1000006", "MSCU1000001");
    }

    [Fact]
    public async Task Inventory_Should_Apply_Gate_In_Dates_Dwell_Days_And_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var lateOctober = await InventoryAsync(dbContext, new GetYardInventoryQuery(
            GateInFrom: October15, GateInTo: new DateOnly(2026, 10, 20)));
        var lateOctoberRows = await YardAsync(dbContext, new SearchYardContainersQuery(
            GateInFrom: October15, GateInTo: new DateOnly(2026, 10, 20)));
        var longMsc = await InventoryAsync(dbContext, new GetYardInventoryQuery(OperatorId: seed.Msc.Id, MinDwellDays: 10));

        // C2 (15/10), C6 second visit (16/10), C3 (20/10); C5 and C6's first visit have left
        lateOctober.Total.Should().Be(3);
        lateOctoberRows.TotalCount.Should().Be(3);
        longMsc.LineOperators.Should().ContainSingle().Which.Total.Should().Be(1);
    }

    [Fact]
    public async Task Throughput_Should_Count_Each_Event_By_Its_Own_Time_And_Split_Gate_Out_Purpose()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);
        var october = new GetThroughputQuery(October1, October15);

        var report = await ThroughputAsync(dbContext, october);

        // Gate-ins: C1, C4, C6 (first visit), C2. Gate-outs: C5 (came in in September) and C6 (to repair)
        report.TotalGateIn.Should().Be(4);
        report.TotalGateOut.Should().Be(2);
        report.TotalReleased.Should().Be(1);
        report.TotalToRepair.Should().Be(1);
        report.LineOperators.Select(o => (o.Code, o.GateIn, o.GateOut, o.Released, o.ToRepair)).Should().Equal(
            ("CMA", 1, 1, 1, 0),
            ("MSC", 3, 1, 0, 1));
    }

    [Fact]
    public async Task Throughput_Detail_Should_Match_Totals_And_List_Newest_Event_First()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var gateIns = await EventsAsync(dbContext, new GetThroughputEventsQuery(ThroughputEventKind.GateIn, October1, October15));
        var gateOuts = await EventsAsync(dbContext, new GetThroughputEventsQuery(ThroughputEventKind.GateOut, October1, October15));
        var toRepair = await ThroughputAsync(dbContext, new GetThroughputQuery(October1, October15, Purpose: GateOutPurpose.ExternalRepair));
        var toRepairRows = await EventsAsync(dbContext, new GetThroughputEventsQuery(
            ThroughputEventKind.GateOut, October1, October15, Purpose: GateOutPurpose.ExternalRepair));
        var reefer = await ThroughputAsync(dbContext, new GetThroughputQuery(October1, October15, Category: ContainerCategory.Reefer));

        gateIns.TotalCount.Should().Be(4);
        gateIns.Items.Select(i => i.ContainerNumber).Should().Equal("MSCU1000002", "MSCU1000006", "CMAU1000004", "MSCU1000001");

        gateOuts.TotalCount.Should().Be(2);
        gateOuts.Items.Select(i => i.ContainerNumber).Should().Equal("MSCU1000006", "CMAU1000005");
        var release = gateOuts.Items[1];
        release.ExitPurpose.Should().Be(GateOutPurpose.EmptyRelease);
        release.DeliveryOrderNumber.Should().Be("DO-001");
        release.DwellDays.Should().Be(15);   // 25/09 -> 10/10

        // The purpose filter narrows gate-outs in the totals and the rows alike, never the gate-ins
        toRepair.TotalGateOut.Should().Be(1);
        toRepair.TotalGateIn.Should().Be(4);
        toRepairRows.TotalCount.Should().Be(1);

        reefer.TotalGateIn.Should().Be(1);
        reefer.TotalGateOut.Should().Be(0);
    }

    [Fact]
    public async Task History_Should_List_One_Row_Per_Visit_With_Its_Own_Operator_And_Owner()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var c6 = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(Search: "mscu1000006"));
        var c6ById = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(ContainerId: seed.C6.Id));
        var c1 = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(ContainerId: seed.C1.Id));
        var c3 = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(ContainerId: seed.C3.Id));
        var cma = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(OperatorId: seed.Cma.Id));

        // A container that came back has one row per visit, newest first
        c6.TotalCount.Should().Be(2);
        c6.Items.Select(i => i.VisitId).Should().Equal(seed.C6Second.Id, seed.C6First.Id);
        c6.Items[0].IsOpen.Should().BeTrue();
        c6.Items[0].DwellDays.Should().Be(5);
        c6.Items[1].ExitPurpose.Should().Be(GateOutPurpose.ExternalRepair);
        c6.Items[1].DwellDays.Should().Be(2);
        c6ById.TotalCount.Should().Be(2);

        // Owner at gate-in only: an owner recorded later is not shown on the older visit
        c1.Items.Single().OwnerCode.Should().Be("HMM");
        c3.Items.Single().OwnerCode.Should().BeNull();

        cma.Items.Should().OnlyContain(i => i.LineOperatorCode == "CMA").And.HaveCount(2);
    }

    [Fact]
    public async Task History_Period_Should_Apply_To_The_Chosen_Event()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedAsync(dbContext);

        var gateIn = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(From: October1, To: October15, Event: VisitHistoryEvent.GateIn));
        var gateOut = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(From: October1, To: October15, Event: VisitHistoryEvent.GateOut));
        var any = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(From: October1, To: October15));
        var open = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(State: VisitState.Open));
        var closed = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(State: VisitState.Closed));
        var repair = await HistoryAsync(dbContext, new SearchVisitHistoryQuery(Purpose: GateOutPurpose.ExternalRepair));

        gateIn.TotalCount.Should().Be(4);
        gateOut.Items.Select(i => i.ContainerNumber).Should().Equal("MSCU1000006", "CMAU1000005");
        // C6's first visit has both events in the period: still one row
        any.TotalCount.Should().Be(5);
        any.Items.Select(i => i.VisitId).Should().OnlyHaveUniqueItems();
        open.TotalCount.Should().Be(5);
        closed.TotalCount.Should().Be(2);
        repair.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Container_Summary_Should_Count_Visits_And_Sum_Dwell_Days_Inside_The_Depot()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var never = new Container("TGHU0000001", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "OWNER");
        dbContext.Containers.Add(never);
        await dbContext.SaveChangesAsync(Token);
        var handler = new GetContainerVisitSummaryQueryHandler(dbContext, Clock);

        var c6 = (await handler.HandleAsync(new GetContainerVisitSummaryQuery(seed.C6.Id), Token)).Value!;
        var c5 = (await handler.HandleAsync(new GetContainerVisitSummaryQuery(seed.C5.Id), Token)).Value!;
        var neverIn = (await handler.HandleAsync(new GetContainerVisitSummaryQuery(never.Id), Token)).Value!;
        var unknown = await handler.HandleAsync(new GetContainerVisitSummaryQuery(Guid.NewGuid()), Token);

        // 12/10 -> 14/10 = 2 days, then 16/10 -> today = 5 days; the two days outside are not counted
        (c6.GateInCount, c6.GateOutCount, c6.Presence, c6.CurrentVisitId, c6.CurrentDwellDays, c6.TotalDwellDays)
            .Should().Be((2, 1, ContainerPresence.InDepot, (Guid?)seed.C6Second.Id, (int?)5, 7));
        (c5.Presence, c5.CurrentDwellDays, c5.TotalDwellDays).Should().Be((ContainerPresence.OutOfDepot, (int?)null, 15));
        (neverIn.Presence, neverIn.GateInCount, neverIn.TotalDwellDays).Should().Be((ContainerPresence.NeverIn, 0, 0));
        unknown.Error!.Code.Should().Be("Container.NotFound");
    }

    [Fact]
    public async Task Visit_Detail_Should_Show_Gate_Events_And_Stored_Documents()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = new GetVisitHistoryQueryHandler(dbContext, Clock);

        var released = (await handler.HandleAsync(new GetVisitHistoryQuery(seed.C5Visit.Id), Token)).Value!;
        var missing = await handler.HandleAsync(new GetVisitHistoryQuery(Guid.NewGuid()), Token);

        released.GateIn.Position!.BlockCode.Should().Be("WAIT");
        released.GateOut!.DeliveryOrderNumber.Should().Be("DO-001");
        released.GateOut.CustomerName.Should().Be("Customer A");
        released.CurrentPosition.Should().BeNull();
        released.Documents.Should().ContainSingle(d => d.Kind == VisitDocumentKind.DeliveryOrder && d.Reference == "DO-001");
        missing.Error!.Code.Should().Be("ContainerVisit.NotFound");
    }

    [Fact]
    public async Task Move_Should_Be_Recorded_In_The_Visit_History()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var msc = DepotTestData.Partner("MSC");
        var yard = Block.CreatePhysical("A", maxBays: 20, maxRows: 4, maxTiers: 3);
        var container = new Container("MSCU2000001", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "OWNER");
        var visit = ContainerVisit.GateIn(container, msc.Id, yard, 1, 1, 1,
            ContainerCondition.Normal, null, "51C-11111", Now.AddDays(-1)).Value!;
        dbContext.AddRange(msc, yard, container, visit);
        await dbContext.SaveChangesAsync(Token);

        var moved = await new MoveContainerCommandHandler(dbContext, Clock, new FakeCurrentUser())
            .HandleAsync(new MoveContainerCommand(container.Id, yard.Id, 3, 1, 1), Token);
        var detail = (await new GetVisitHistoryQueryHandler(dbContext, Clock)
            .HandleAsync(new GetVisitHistoryQuery(visit.Id), Token)).Value!;

        moved.IsSuccess.Should().BeTrue(moved.Error?.Message);
        dbContext.ContainerMoves.Should().ContainSingle();
        var move = detail.Moves.Should().ContainSingle().Subject;
        (move.From.BlockCode, move.From.Bay, move.To.Bay, move.MovedAt).Should().Be(("A", (int?)1, (int?)3, Now));
        // Gate-in position stays where the container came in; the current position follows the move
        detail.GateIn.Position!.Bay.Should().Be(1);
        detail.CurrentPosition!.Bay.Should().Be(3);
    }

    [Fact]
    public void Validators_Should_Reject_Reversed_Periods_And_Bad_Pages()
    {
        new SearchVisitHistoryValidator()
            .Validate(new SearchVisitHistoryQuery(From: October15, To: October1)).IsValid.Should().BeFalse();
        new GetYardInventoryValidator()
            .Validate(new GetYardInventoryQuery(GateInFrom: October15, GateInTo: October1)).IsValid.Should().BeFalse();
        new GetYardInventoryValidator()
            .Validate(new GetYardInventoryQuery(MinDwellDays: 10, MaxDwellDays: 3)).IsValid.Should().BeFalse();
        new GetThroughputEventsValidator()
            .Validate(new GetThroughputEventsQuery(ThroughputEventKind.GateIn, October1, October15, PageSize: 0))
            .IsValid.Should().BeFalse();
        new GetThroughputEventsValidator()
            .Validate(new GetThroughputEventsQuery(ThroughputEventKind.GateOut, October1, October15))
            .IsValid.Should().BeTrue();
    }
}
