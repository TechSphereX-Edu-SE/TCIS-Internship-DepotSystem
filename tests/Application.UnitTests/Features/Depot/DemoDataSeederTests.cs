using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence.Demo;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

/*
    Feedback 10: the demo dataset. Each test has its own in-memory database and a fixed clock, so it never
    touches the demo database of a developer and gives the same result on every day it runs.
*/
public sealed class DemoDataSeederTests
{
    // 23/10/2026 10:00 in Vietnam: the anchor of the first seed in these tests
    private static readonly DateTimeOffset Anchor = new(2026, 10, 23, 10, 0, 0, TimeSpan.FromHours(7));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DemoDataSeeder Seeder(AppDbContext dbContext, DateTimeOffset now) =>
        new(dbContext, new FixedTimeProvider(now), NullLogger<DemoDataSeeder>.Instance);

    private static IEnumerable<DemoContainerPlan> Plans(DemoScenario scenario) =>
        DemoCatalog.Containers.Where(p => p.Scenario == scenario);

    private static Task<List<ContainerVisit>> VisitsOfAsync(AppDbContext dbContext, DemoContainerPlan plan) =>
        dbContext.ContainerVisits.Where(v => v.Container.ContainerNumber == plan.Number)
            .OrderBy(v => v.GateInAt).ToListAsync(Token);

    private static Task<RepairCase> CaseOfAsync(AppDbContext dbContext, DemoContainerPlan plan) =>
        dbContext.RepairCases.SingleAsync(c => c.Container.ContainerNumber == plan.Number, Token);

    [Fact]
    public void Catalog_has_140_containers_with_unique_valid_numbers_and_iso_codes()
    {
        var plans = DemoCatalog.Containers;

        plans.Should().HaveCount(140);
        plans.Select(p => p.Number).Should().OnlyHaveUniqueItems();
        plans.Should().OnlyContain(p => ContainerNumberRules.IsValid(p.Number));
        plans.Should().OnlyContain(p => IsoCodeRules.IsValid(p.Type.IsoFor(p.Size), p.Size, p.Type.Category));

        // 5 operators x 7 types x (2 x 20ft + 2 x 40ft)
        plans.GroupBy(p => (p.Operator.Code, p.Type.Category, p.Size)).Should().HaveCount(5 * 7 * 2)
            .And.OnlyContain(g => g.Count() == 2);
        plans.Select(p => p.Scenario).Distinct().Should().HaveCount(Enum.GetValues<DemoScenario>().Length);
    }

    [Fact]
    public void Container_numbers_are_stable()
    {
        // The guide (docs/demo-data.md) lists these numbers: they must never change
        DemoCatalog.Containers[0].Number.Should().Be("MSCU8000019");
        DemoCatalog.Containers[2].Number.Should().Be("MSCU800003X");
        DemoCatalog.Containers[139].Number.Should().Be("EMCU8000642");
    }

    [Fact]
    public async Task First_seed_creates_every_scenario_through_the_business_rules()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var report = await Seeder(dbContext, Anchor).SeedAsync(Token);

        report.Failures.Should().BeEmpty();
        report.AnchorCreated.Should().BeTrue();
        report.AnchorAt.Should().Be(Anchor);
        report.ContainersCreated.Should().Be(140);
        (await dbContext.Containers.CountAsync(Token)).Should().Be(140);

        // Never in: catalog row and operating assignment only
        foreach (var plan in Plans(DemoScenario.NeverIn))
            (await VisitsOfAsync(dbContext, plan)).Should().BeEmpty();

        // Dwell time on the anchor day: 0, 9, 10, 11 days, around the 10-day long-stay limit
        foreach (var (scenario, days) in new[]
                 {
                     (DemoScenario.InYardToday, 0), (DemoScenario.InYard9Days, 9),
                     (DemoScenario.InYard10Days, 10), (DemoScenario.InYard11Days, 11), (DemoScenario.LongStay, 75)
                 })
        foreach (var plan in Plans(scenario))
        {
            var visit = (await VisitsOfAsync(dbContext, plan)).Should().ContainSingle().Subject;
            visit.IsInYard.Should().BeTrue();
            DepotTime.DwellDays(visit.GateInAt, Anchor).Should().Be(days);
            visit.GateInAt.Should().BeOnOrBefore(Anchor);
        }

        // Released, and back several times: every return is a new visit
        foreach (var plan in Plans(DemoScenario.Released))
            (await VisitsOfAsync(dbContext, plan)).Should().ContainSingle(v => !v.IsInYard && v.DeliveryOrderId != null);
        foreach (var plan in Plans(DemoScenario.ReturnedManyTimes))
        {
            var visits = await VisitsOfAsync(dbContext, plan);
            visits.Should().HaveCount(3);
            visits.Take(2).Should().OnlyContain(v => !v.IsInYard);
            visits[2].IsInYard.Should().BeTrue();
        }
    }

    [Fact]
    public async Task First_seed_covers_each_repair_state()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await Seeder(dbContext, Anchor).SeedAsync(Token);

        foreach (var (scenario, status) in new[]
                 {
                     (DemoScenario.DamagedAwaitingInspection, RepairCaseStatus.AwaitingInspection),
                     (DemoScenario.InspectedNoRepair, RepairCaseStatus.Passed),
                     (DemoScenario.AwaitingEstimateApproval, RepairCaseStatus.AwaitingEstimateApproval),
                     (DemoScenario.InRepair, RepairCaseStatus.InRepair),
                     (DemoScenario.AwaitingAcceptance, RepairCaseStatus.AwaitingAcceptance),
                     (DemoScenario.RepairedAndReleased, RepairCaseStatus.Passed),
                     (DemoScenario.AtOutsideRepair, RepairCaseStatus.RepairApproved),
                     (DemoScenario.BackFromOutsideRepair, RepairCaseStatus.Passed)
                 })
        foreach (var plan in Plans(scenario))
            (await CaseOfAsync(dbContext, plan)).Status.Should().Be(status, $"{plan.Number} follows {scenario}");

        // Refused first estimate, second version waiting for the line operator
        var waiting = await CaseOfAsync(dbContext, Plans(DemoScenario.AwaitingEstimateApproval).First());
        (await dbContext.RepairEstimates.Where(e => e.RepairCaseId == waiting.Id).OrderBy(e => e.Version)
                .Select(e => e.Status).ToListAsync(Token))
            .Should().Equal(RepairEstimateStatus.Rejected, RepairEstimateStatus.Submitted);

        // The customer disputes the gasket item of a case being repaired
        var inRepair = await CaseOfAsync(dbContext, Plans(DemoScenario.InRepair).First());
        var estimateId = await dbContext.RepairEstimates.Where(e => e.RepairCaseId == inRepair.Id).Select(e => e.Id).SingleAsync(Token);
        (await dbContext.RepairEstimateLines.SingleAsync(l => l.EstimateId == estimateId && l.Position == 2, Token))
            .PayerStatus.Should().Be(CostPayerStatus.Disputed);

        // At the outside facility: out of the yard; back from it: a second visit, transfer closed
        foreach (var plan in Plans(DemoScenario.AtOutsideRepair))
        {
            (await VisitsOfAsync(dbContext, plan)).Should().ContainSingle(v => !v.IsInYard);
            (await dbContext.ExternalRepairTransfers.SingleAsync(t => t.Container.ContainerNumber == plan.Number, Token))
                .Status.Should().Be(ExternalRepairStatus.GatedOut);
        }
        foreach (var plan in Plans(DemoScenario.BackFromOutsideRepair))
        {
            (await VisitsOfAsync(dbContext, plan)).Should().HaveCount(2);
            (await dbContext.ExternalRepairTransfers.SingleAsync(t => t.Container.ContainerNumber == plan.Number, Token))
                .Status.Should().Be(ExternalRepairStatus.Returned);
        }
    }

    [Fact]
    public async Task Rows_written_by_the_seed_carry_the_time_of_their_event()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await Seeder(dbContext, Anchor).SeedAsync(Token);

        // A release 100 days ago: its order is created 2 hours before the gate-out, not "today"
        var plan = Plans(DemoScenario.ReturnedManyTimes).First();
        var firstExit = (await VisitsOfAsync(dbContext, plan))[0];
        var order = await dbContext.DeliveryOrders.SingleAsync(o => o.Id == firstExit.DeliveryOrderId, Token);

        DepotTime.DwellDays(firstExit.GateOutAt!.Value, Anchor).Should().Be(100);
        order.CreatedAt.Should().Be(firstExit.GateOutAt.Value.AddHours(-2));
        firstExit.CreatedAt.Should().Be(firstExit.GateInAt);
    }

    [Fact]
    public async Task Running_again_later_adds_nothing_and_keeps_the_anchor_and_dates()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await Seeder(dbContext, Anchor).SeedAsync(Token);
        var visitsBefore = await dbContext.ContainerVisits.OrderBy(v => v.Id).Select(v => new { v.Id, v.GateInAt, v.GateOutAt })
            .ToListAsync(Token);
        var eventsBefore = await dbContext.RepairCaseEvents.CountAsync(Token);

        var report = await Seeder(dbContext, Anchor.AddDays(5)).SeedAsync(Token);

        report.AnchorCreated.Should().BeFalse();
        report.AnchorAt.Should().Be(Anchor);
        report.ContainersCreated.Should().Be(0);
        report.ContainersSkipped.Should().Be(140);
        (await dbContext.ContainerVisits.OrderBy(v => v.Id).Select(v => new { v.Id, v.GateInAt, v.GateOutAt })
            .ToListAsync(Token)).Should().Equal(visitsBefore);
        (await dbContext.RepairCaseEvents.CountAsync(Token)).Should().Be(eventsBefore);
        (await dbContext.LineOperators.CountAsync(Token)).Should().Be(5);
        (await dbContext.Blocks.CountAsync(Token)).Should().Be(3);
    }

    [Fact]
    public async Task Running_again_keeps_what_users_did_with_demo_containers()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await Seeder(dbContext, Anchor).SeedAsync(Token);

        // A user closes the inspection of a container the demo left waiting
        var plan = Plans(DemoScenario.DamagedAwaitingInspection).First();
        var repairCase = await CaseOfAsync(dbContext, plan);
        var inspected = await new ApplyRepairActionCommandHandler(dbContext, new FixedTimeProvider(Anchor.AddDays(1)), new FakeCurrentUser())
            .HandleAsync(new ApplyRepairActionCommand(repairCase.Id, RepairAction.InspectPassed, null), Token);
        inspected.IsSuccess.Should().BeTrue();

        await Seeder(dbContext, Anchor.AddDays(2)).SeedAsync(Token);

        (await CaseOfAsync(dbContext, plan)).Status.Should().Be(RepairCaseStatus.Passed);
    }

    [Fact]
    public async Task A_missing_container_is_added_with_the_dates_of_the_first_anchor()
    {
        // Same anchor, but the seed runs a month later (e.g. a container added to the catalog afterwards)
        await using var dbContext = TestDbContextFactory.Create();
        dbContext.DemoDatasets.Add(DemoDataset.Start(DemoCatalog.DatasetKey, Anchor));
        await dbContext.SaveChangesAsync(Token);

        var report = await Seeder(dbContext, Anchor.AddDays(30)).SeedAsync(Token);

        report.AnchorCreated.Should().BeFalse();
        report.Failures.Should().BeEmpty();
        var plan = Plans(DemoScenario.InYard9Days).First();
        var visit = (await VisitsOfAsync(dbContext, plan)).Should().ContainSingle().Subject;
        DepotTime.DateOf(visit.GateInAt).Should().Be(DepotTime.DateOf(Anchor).AddDays(-9));
    }

    [Fact]
    public async Task Reset_deletes_depot_data_and_seeds_again_with_a_new_anchor()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await Seeder(dbContext, Anchor).SeedAsync(Token);
        dbContext.Customers.Add(new Customer("0300000001", "Khách hàng do người dùng tạo"));
        dbContext.Todos.Add(new TodoItem { Title = "Giữ lại" });
        await dbContext.SaveChangesAsync(Token);

        var newAnchor = Anchor.AddDays(7);
        var report = await Seeder(dbContext, newAnchor).ResetAsync(Token);

        report.AnchorCreated.Should().BeTrue();
        report.AnchorAt.Should().Be(newAnchor);
        report.Failures.Should().BeEmpty();
        (await dbContext.Containers.CountAsync(Token)).Should().Be(140);
        (await dbContext.Customers.AnyAsync(c => c.TaxCode == "0300000001", Token)).Should().BeFalse();
        (await dbContext.Todos.CountAsync(Token)).Should().Be(1);
        (await dbContext.DemoDatasets.CountAsync(Token)).Should().Be(1);
    }
}
