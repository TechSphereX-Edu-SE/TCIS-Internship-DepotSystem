using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetInYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.SearchYard;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Application.Features.RepairCases.Act;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Feedback 5 + 8: yard search with filters, repair markers on the map / lookup, move from a checked position
public sealed class YardSearchAndMarkerTests
{
    // 22/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.FromHours(7));

    private sealed record Seed(LineOperator Msc, LineOperator Cma, Block A, Block Repair);

    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(DepotTestData.Partner("MSC"), DepotTestData.Partner("CMA"),
            Block.CreatePhysical("A", maxBays: 20, maxRows: 4, maxTiers: 3), Block.CreateVirtual("REPAIR"));
        dbContext.AddRange(seed.Msc, seed.Cma, seed.A, seed.Repair);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    // Gate-in through the real handler at the given time, so dwell days and repair cases are real
    private static async Task<GateInResponse> GateInAsync(
        AppDbContext dbContext, Seed seed, string number, int? bay, DateTimeOffset at,
        ContainerSize size = ContainerSize.Ft20, ContainerCategory category = ContainerCategory.Dry,
        ContainerCondition condition = ContainerCondition.Normal, LineOperator? lineOperator = null, Block? block = null)
    {
        var container = new Container(number, size == ContainerSize.Ft40 ? "42G1" : "22G1", size, category, 30480, 2200, null, "MSC");
        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await DepotTestData.AssignAsync(dbContext, container, lineOperator ?? seed.Msc);

        var target = block ?? seed.A;
        var result = await new GateInCommandHandler(dbContext, new FixedTimeProvider(at), new FakeCurrentUser()).HandleAsync(
            new GateInCommand(container.Id, (lineOperator ?? seed.Msc).Id, target.Id,
                target.IsVirtual ? null : bay, target.IsVirtual ? null : 1, target.IsVirtual ? null : 1,
                condition, condition == ContainerCondition.Damaged ? "Móp cửa" : null, "51C-11111"),
            TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value!;
    }

    private static Task<Result<PagedResult<YardContainerResponse>>> SearchAsync(
        AppDbContext dbContext, SearchYardContainersQuery query) =>
        new SearchYardContainersQueryHandler(dbContext, new FixedTimeProvider(Now)).HandleAsync(query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Search_Should_Filter_Before_Paging_And_Rank_The_Number()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, "MSCU1234566", 1, Now.AddDays(-12));
        await GateInAsync(dbContext, seed, "MSCU1234500", 3, Now);
        await GateInAsync(dbContext, seed, "XMSCU123456", 5, Now.AddDays(-3), category: ContainerCategory.Reefer);
        await GateInAsync(dbContext, seed, "CMAU7654327", 8, Now.AddDays(-1), ContainerSize.Ft40, lineOperator: seed.Cma);
        await GateInAsync(dbContext, seed, "MSCU0000019", null, Now, block: seed.Repair);

        var all = await SearchAsync(dbContext, new SearchYardContainersQuery());
        var ranked = await SearchAsync(dbContext, new SearchYardContainersQuery(Search: "mscu1234566"));
        var prefix = await SearchAsync(dbContext, new SearchYardContainersQuery(Search: "MSCU123"));
        var dry20 = await SearchAsync(dbContext, new SearchYardContainersQuery(Category: ContainerCategory.Dry, Size: ContainerSize.Ft20));
        var cma = await SearchAsync(dbContext, new SearchYardContainersQuery(OperatorId: seed.Cma.Id));
        var repairBlock = await SearchAsync(dbContext, new SearchYardContainersQuery(BlockId: seed.Repair.Id));
        var longStay = await SearchAsync(dbContext, new SearchYardContainersQuery(MinDwellDays: 10));
        var recent = await SearchAsync(dbContext, new SearchYardContainersQuery(MaxDwellDays: 1));
        var paged = await SearchAsync(dbContext, new SearchYardContainersQuery(PageSize: 2, Page: 2));

        all.Value!.TotalCount.Should().Be(5);
        // Without a search: block, row, bay, tier (the virtual block REPAIR comes after A)
        all.Value.Items.Select(c => c.ContainerNumber).Should()
            .Equal("MSCU1234566", "MSCU1234500", "XMSCU123456", "CMAU7654327", "MSCU0000019");
        ranked.Value!.Items[0].ContainerNumber.Should().Be("MSCU1234566");
        // Prefix matches before the number that only contains the text
        prefix.Value!.Items.Select(c => c.ContainerNumber).Should().Equal("MSCU1234500", "MSCU1234566", "XMSCU123456");
        dry20.Value!.Items.Select(c => c.ContainerNumber).Should().BeEquivalentTo("MSCU1234566", "MSCU1234500", "MSCU0000019");
        cma.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "CMAU7654327");
        repairBlock.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "MSCU0000019" && c.Bay == null);
        longStay.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "MSCU1234566");
        recent.Value!.Items.Select(c => c.ContainerNumber).Should().BeEquivalentTo("MSCU1234500", "CMAU7654327", "MSCU0000019");
        paged.Value!.Items.Should().HaveCount(2);
        paged.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Markers_Should_Tell_Awaiting_Inspection_From_Repair_And_Clear_When_Passed()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var abnormal = await GateInAsync(dbContext, seed, "MSCU1234566", 1, Now, condition: ContainerCondition.Damaged);
        var repaired = await GateInAsync(dbContext, seed, "MSCU1234500", 3, Now, condition: ContainerCondition.Damaged);
        await GateInAsync(dbContext, seed, "MSCU0000019", 5, Now);
        var act = new ApplyRepairActionCommandHandler(dbContext, new FixedTimeProvider(Now), new FakeCurrentUser());
        await act.HandleAsync(new ApplyRepairActionCommand(repaired.RepairCaseId!.Value, RepairAction.InspectNeedsRepair, null),
            TestContext.Current.CancellationToken);

        var map = await new GetContainersInYardQueryHandler(dbContext)
            .HandleAsync(new GetContainersInYardQuery(seed.A.Id, 1), TestContext.Current.CancellationToken);
        var onlyAbnormal = await SearchAsync(dbContext, new SearchYardContainersQuery(Repair: YardRepairState.Abnormal));
        var onlyRepair = await SearchAsync(dbContext, new SearchYardContainersQuery(Repair: YardRepairState.InRepair));
        var clean = await SearchAsync(dbContext, new SearchYardContainersQuery(Repair: YardRepairState.None));
        var position = await new GetContainerPositionQueryHandler(dbContext)
            .HandleAsync(new GetContainerPositionQuery("MSCU1234566"), TestContext.Current.CancellationToken);

        var byNumber = map.Value!.ToDictionary(c => c.ContainerNumber);
        byNumber["MSCU1234566"].RepairStatus.Should().Be(RepairCaseStatus.AwaitingInspection);
        byNumber["MSCU1234566"].RepairNote.Should().Be("Móp cửa");
        byNumber["MSCU1234500"].RepairStatus.Should().Be(RepairCaseStatus.AwaitingEstimateApproval);
        byNumber["MSCU0000019"].RepairCaseId.Should().BeNull();
        onlyAbnormal.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "MSCU1234566");
        onlyRepair.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "MSCU1234500");
        clean.Value!.Items.Should().ContainSingle(c => c.ContainerNumber == "MSCU0000019");
        position.Value!.RepairCaseId.Should().Be(abnormal.RepairCaseId);
        position.Value.Condition.Should().Be(ContainerCondition.Damaged);

        // Passed closes the case: the marker goes, the history stays
        await act.HandleAsync(new ApplyRepairActionCommand(abnormal.RepairCaseId!.Value, RepairAction.InspectPassed, null),
            TestContext.Current.CancellationToken);
        var after = await new GetContainerPositionQueryHandler(dbContext)
            .HandleAsync(new GetContainerPositionQuery("MSCU1234566"), TestContext.Current.CancellationToken);

        after.Value!.RepairCaseId.Should().BeNull();
        after.Value.RepairStatus.Should().BeNull();
        dbContext.RepairCaseEvents.Where(e => e.RepairCaseId == abnormal.RepairCaseId).Select(e => e.Kind)
            .Should().Contain([RepairEventKind.Opened, RepairEventKind.InspectPassed]);
    }

    [Fact]
    public async Task Move_Should_Refuse_A_Position_That_Changed_And_Keep_The_Case_When_Moving_To_Repair()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var damaged = await GateInAsync(dbContext, seed, "MSCU1234566", 1, Now, condition: ContainerCondition.Damaged);
        var containerId = dbContext.Containers.Single().Id;
        var handler = new MoveContainerCommandHandler(dbContext, new FixedTimeProvider(Now));

        // Seen at A 1-1-1, then someone moved it to A 3-1-1
        await handler.HandleAsync(new MoveContainerCommand(containerId, seed.A.Id, 3, 1, 1), TestContext.Current.CancellationToken);
        var stale = await handler.HandleAsync(
            new MoveContainerCommand(containerId, seed.Repair.Id, null, null, null, seed.A.Id, 1, 1, 1),
            TestContext.Current.CancellationToken);
        var checkedMove = await handler.HandleAsync(
            new MoveContainerCommand(containerId, seed.Repair.Id, null, null, null, seed.A.Id, 3, 1, 1),
            TestContext.Current.CancellationToken);

        stale.Error!.Code.Should().Be("ContainerVisit.PositionChanged");
        checkedMove.Value!.BlockCode.Should().Be("REPAIR");
        // Moving to the repair area does not change the case: it stays open with its marker
        checkedMove.Value.RepairCaseId.Should().Be(damaged.RepairCaseId);
        checkedMove.Value.RepairStatus.Should().Be(RepairCaseStatus.AwaitingInspection);
    }

    [Fact]
    public void Validator_Should_Refuse_An_Empty_Dwell_Range()
    {
        var validator = new SearchYardContainersValidator();

        validator.Validate(new SearchYardContainersQuery(MinDwellDays: 3, MaxDwellDays: 10)).IsValid.Should().BeTrue();
        validator.Validate(new SearchYardContainersQuery(MinDwellDays: 10, MaxDwellDays: 3)).IsValid.Should().BeFalse();
        validator.Validate(new SearchYardContainersQuery(PageSize: 0)).IsValid.Should().BeFalse();
    }
}
