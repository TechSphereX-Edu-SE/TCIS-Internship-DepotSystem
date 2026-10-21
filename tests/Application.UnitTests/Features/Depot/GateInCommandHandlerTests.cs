using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class GateInCommandHandlerTests
{
    // Fixed clock so the recorded gate-in time can be asserted
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(7));

    private sealed record Seed(LineOperator LineOperator, Block Physical, Block Virtual, Container Ft20, Container Ft40);

    // One line operator, a physical block (10 bays, 4 rows, 3 tiers), a virtual block, a 20ft and a 40ft container
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(
            new LineOperator("MSC", "Mediterranean Shipping Company"),
            Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3),
            Block.CreateVirtual("REPAIR"),
            new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC"),
            new Container("CMAU1234564", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3800, null, "CMA"));

        dbContext.AddRange(seed.LineOperator, seed.Physical, seed.Virtual, seed.Ft20, seed.Ft40);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    private static GateInCommandHandler Handler(AppDbContext dbContext) => new(dbContext, new FixedTimeProvider(Now));

    private static GateInCommand Command(Container container, Seed seed, Block block, int? bay, int? row, int? tier) =>
        new(container.Id, seed.LineOperator.Id, block.Id, bay, row, tier,
            ContainerGrade.A, ContainerCondition.Normal, null, "51c-12345");

    [Fact]
    public async Task Should_Gate_In_To_Free_Physical_Slot()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await Handler(dbContext).HandleAsync(
            Command(seed.Ft20, seed, seed.Physical, 5, 3, 1), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BlockCode.Should().Be("A");
        result.Value.GateInAt.Should().Be(Now);
        var visit = dbContext.ContainerVisits.Single();
        visit.GateInVehicleNo.Should().Be("51C-12345");
        visit.IsInYard.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Gate_In_To_Virtual_Block_Without_Position()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);

        var first = await handler.HandleAsync(Command(seed.Ft20, seed, seed.Virtual, null, null, null), TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(Command(seed.Ft40, seed, seed.Virtual, null, null, null), TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue(); // a virtual block has no slots to collide
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Slot_Is_Taken()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);
        await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 5, 3, 1), TestContext.Current.CancellationToken);

        // A second 20ft container tries the same slot 5-3-1
        var other20 = new Container("CMAU100000X", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "CMA");
        dbContext.Containers.Add(other20);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(Command(other20, seed, seed.Physical, 5, 3, 1), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("ContainerVisit.SlotOccupied");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Container_Is_Already_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);
        await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 5, 3, 1), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 7, 1, 1), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("ContainerVisit.AlreadyInYard");
    }

    [Fact]
    public async Task Should_Return_Validation_When_Slot_Does_Not_Fit_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        // 40ft must be in an even bay
        var result = await Handler(dbContext).HandleAsync(
            Command(seed.Ft40, seed, seed.Physical, 3, 1, 1), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        dbContext.ContainerVisits.Should().BeEmpty();
    }

    // Adds an extra 20ft Dry container so a test can fill more than one slot
    private static async Task<Container> AddFt20Async(AppDbContext dbContext, string number)
    {
        var container = new Container(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "HMM");
        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return container;
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Ft40_Overlaps_Ft20_Next_To_It()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);
        await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 5, 1, 1), TestContext.Current.CancellationToken);

        // 40ft in bay 4 covers bays 3 and 5
        var result = await handler.HandleAsync(Command(seed.Ft40, seed, seed.Physical, 4, 1, 1), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("ContainerVisit.SlotOccupied");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Nothing_Underneath()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await Handler(dbContext).HandleAsync(
            Command(seed.Ft20, seed, seed.Physical, 5, 1, 2), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("ContainerVisit.NoSupportBelow");
    }

    [Fact]
    public async Task Should_Stack_Ft40_On_Two_Ft20_But_Not_On_One()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);
        var second20 = await AddFt20Async(dbContext, "HMMU0000016");
        await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 3, 1, 1), TestContext.Current.CancellationToken);

        // Only bay 3 is covered below; bay 5 is empty
        var onOne = await handler.HandleAsync(Command(seed.Ft40, seed, seed.Physical, 4, 1, 2), TestContext.Current.CancellationToken);
        await handler.HandleAsync(Command(second20, seed, seed.Physical, 5, 1, 1), TestContext.Current.CancellationToken);
        var onTwo = await handler.HandleAsync(Command(seed.Ft40, seed, seed.Physical, 4, 1, 2), TestContext.Current.CancellationToken);

        onOne.Error!.Code.Should().Be("ContainerVisit.NoSupportBelow");
        onTwo.IsSuccess.Should().BeTrue();
        dbContext.ContainerVisits.Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_Allow_Ft20_In_Other_Row_Next_To_Ft40()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var handler = Handler(dbContext);
        await handler.HandleAsync(Command(seed.Ft40, seed, seed.Physical, 4, 1, 1), TestContext.Current.CancellationToken);

        // Overlap only counts within the same row
        var result = await handler.HandleAsync(Command(seed.Ft20, seed, seed.Physical, 5, 2, 1), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Block_Does_Not_Exist()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var command = Command(seed.Ft20, seed, seed.Physical, 5, 3, 1) with { BlockId = Guid.NewGuid() };

        var result = await Handler(dbContext).HandleAsync(command, TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_Should_Require_Full_Or_Empty_Position_And_Vehicle()
    {
        var validator = new GateInValidator();
        var valid = new GateInCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, 3, 1,
            ContainerGrade.A, ContainerCondition.Normal, null, "51C-12345");

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { Bay = null, Row = null, Tier = null }).IsValid.Should().BeTrue(); // virtual block
        validator.Validate(valid with { Tier = null }).IsValid.Should().BeFalse();                        // partial position
        validator.Validate(valid with { Row = 0 }).IsValid.Should().BeFalse();
        validator.Validate(valid with { VehicleNo = "" }).IsValid.Should().BeFalse();
    }
}
