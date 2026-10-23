using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Move;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class MoveContainerCommandHandlerTests
{
    private sealed record Seed(LineOperator LineOperator, Block Physical, Block Repair, Container X20, Container Y20, Container Z40);

    // A physical block (10 bays, 4 rows, 3 tiers), a virtual repair block, two 20ft and one 40ft container
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(
            new LineOperator("MSC", "Mediterranean Shipping Company"),
            Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3),
            Block.CreateVirtual("REPAIR"),
            new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC"),
            new Container("HMMU0000016", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "HMM"),
            new Container("CMAU1234564", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3800, null, "CMA"));

        dbContext.AddRange(seed.LineOperator, seed.Physical, seed.Repair, seed.X20, seed.Y20, seed.Z40,
            DepotTestData.Confirmed(seed.X20, seed.LineOperator),
            DepotTestData.Confirmed(seed.Y20, seed.LineOperator),
            DepotTestData.Confirmed(seed.Z40, seed.LineOperator));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    // Puts a container in the yard through the real gate-in handler, so all yard rules apply
    private static async Task GateInAsync(AppDbContext dbContext, Seed seed, Container container, Block block, int? bay, int? row, int? tier)
    {
        var result = await new GateInCommandHandler(dbContext, TimeProvider.System).HandleAsync(
            new GateInCommand(container.Id, seed.LineOperator.Id, block.Id, bay, row, tier,
                ContainerCondition.Normal, null, "51C-12345"),
            TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
    }

    private static Task<Result<ContainerPositionResponse>> MoveAsync(
        AppDbContext dbContext, Container container, Block block, int? bay, int? row, int? tier) =>
        new MoveContainerCommandHandler(dbContext, TimeProvider.System).HandleAsync(
            new MoveContainerCommand(container.Id, block.Id, bay, row, tier), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_Move_To_Free_Slot()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 1, 1, 1);

        var result = await MoveAsync(dbContext, seed.X20, seed.Physical, 3, 2, 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BlockCode.Should().Be("A");
        result.Value.Bay.Should().Be(3);
        result.Value.Row.Should().Be(2);
        dbContext.ContainerVisits.Single().Bay.Should().Be(3);
    }

    [Fact]
    public async Task Should_Move_To_Virtual_Block_And_Back()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 1, 1, 1);

        var toRepair = await MoveAsync(dbContext, seed.X20, seed.Repair, null, null, null);
        var back = await MoveAsync(dbContext, seed.X20, seed.Physical, 5, 1, 1);

        toRepair.Value!.IsVirtualBlock.Should().BeTrue();
        toRepair.Value.Bay.Should().BeNull();
        back.Value!.BlockCode.Should().Be("A");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Target_Block_Does_Not_Take_The_Type()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var reeferOnly = Block.CreatePhysical("B", maxBays: 10, maxRows: 4, maxTiers: 3, allowedCategories: [ContainerCategory.Reefer]);
        dbContext.Blocks.Add(reeferOnly);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 1, 1, 1);

        var result = await MoveAsync(dbContext, seed.X20, reeferOnly, 1, 1, 1);

        result.Error!.Code.Should().Be("Block.CategoryNotAllowed");
        dbContext.ContainerVisits.Single().BlockId.Should().Be(seed.Physical.Id);
    }

    [Fact]
    public async Task Should_Return_Conflict_When_A_Container_Is_Stacked_On_It()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 5, 1, 1);
        await GateInAsync(dbContext, seed, seed.Y20, seed.Physical, 5, 1, 2);

        var bottom = await MoveAsync(dbContext, seed.X20, seed.Physical, 7, 1, 1);
        var top = await MoveAsync(dbContext, seed.Y20, seed.Physical, 7, 1, 1);

        bottom.Error!.Code.Should().Be("ContainerVisit.ContainerAbove");
        top.IsSuccess.Should().BeTrue(); // the top one can always move
    }

    [Fact]
    public async Task Should_Return_Conflict_When_A_40ft_Rests_On_The_20ft()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 3, 1, 1);
        await GateInAsync(dbContext, seed, seed.Y20, seed.Physical, 5, 1, 1);
        await GateInAsync(dbContext, seed, seed.Z40, seed.Physical, 4, 1, 2); // covers bays 3 and 5

        var result = await MoveAsync(dbContext, seed.Y20, seed.Physical, 9, 1, 1);

        result.Error!.Code.Should().Be("ContainerVisit.ContainerAbove");
    }

    [Fact]
    public async Task Should_Not_Stand_On_Its_Own_Old_Place()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 5, 1, 1);

        // Its current place at tier 1 does not count as support once it leaves
        var result = await MoveAsync(dbContext, seed.X20, seed.Physical, 5, 1, 2);

        result.Error!.Code.Should().Be("ContainerVisit.NoSupportBelow");
        dbContext.ContainerVisits.Single().Tier.Should().Be(1);
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Target_Overlaps_A_40ft()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.X20, seed.Physical, 1, 1, 1);
        await GateInAsync(dbContext, seed, seed.Z40, seed.Physical, 6, 1, 1); // covers bays 5 and 7

        var result = await MoveAsync(dbContext, seed.X20, seed.Physical, 5, 1, 1);

        result.Error!.Code.Should().Be("ContainerVisit.SlotOccupied");
        dbContext.ContainerVisits.First(v => v.ContainerId == seed.X20.Id).Bay.Should().Be(1);
    }

    [Fact]
    public async Task Should_Return_Validation_When_Target_Does_Not_Fit_Or_Is_The_Same()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await GateInAsync(dbContext, seed, seed.Z40, seed.Physical, 4, 1, 1);

        var oddBay = await MoveAsync(dbContext, seed.Z40, seed.Physical, 3, 1, 1); // 40ft needs an even bay
        var same = await MoveAsync(dbContext, seed.Z40, seed.Physical, 4, 1, 1);

        oddBay.Error!.Type.Should().Be(ErrorType.Validation);
        same.Error!.Code.Should().Be("ContainerVisit.SamePosition");
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Not_In_Yard_Or_Unknown()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var notInYard = await MoveAsync(dbContext, seed.X20, seed.Physical, 1, 1, 1);
        var unknown = await new MoveContainerCommandHandler(dbContext, TimeProvider.System).HandleAsync(
            new MoveContainerCommand(Guid.NewGuid(), seed.Physical.Id, 1, 1, 1), TestContext.Current.CancellationToken);

        notInYard.Error!.Code.Should().Be("ContainerVisit.NotInYard");
        unknown.Error!.Code.Should().Be("Container.NotFound");
    }

    [Fact]
    public void Validator_Should_Require_Full_Or_Empty_Position()
    {
        var validator = new MoveContainerValidator();
        var valid = new MoveContainerCommand(Guid.NewGuid(), Guid.NewGuid(), 5, 1, 1);

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { Bay = null, Row = null, Tier = null }).IsValid.Should().BeTrue(); // virtual block
        validator.Validate(valid with { Tier = null }).IsValid.Should().BeFalse();
        validator.Validate(valid with { BlockId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
