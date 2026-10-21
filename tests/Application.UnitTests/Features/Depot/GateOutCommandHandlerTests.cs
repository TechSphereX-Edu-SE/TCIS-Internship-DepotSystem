using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateIn;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GateOut;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class GateOutCommandHandlerTests
{
    // 21/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(7));

    private sealed record Seed(
        LineOperator Msc, LineOperator Cma, Customer Customer, Block Yard,
        Container Dry1, Container Dry2, Container Reefer);

    // Two line operators, a customer, a physical block (10 bays, 4 rows, 3 tiers), two 20ft dry and one 20ft reefer
    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(
            new LineOperator("MSC", "Mediterranean Shipping Company"),
            new LineOperator("CMA", "CMA CGM"),
            new Customer("0301234567", "Customer A"),
            Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3),
            new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC"),
            new Container("HMMU0000016", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "HMM"),
            new Container("HMMU3007776", "22R1", ContainerSize.Ft20, ContainerCategory.Reefer, 30480, 3000, null, "HMM"));

        dbContext.AddRange(seed.Msc, seed.Cma, seed.Customer, seed.Yard, seed.Dry1, seed.Dry2, seed.Reefer);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    // Order of MSC with only Dry containers
    private static async Task<DeliveryOrder> AddOrderAsync(
        AppDbContext dbContext, Seed seed, int dryQuantity = 1, DateOnly? expiry = null)
    {
        var order = new DeliveryOrder("DO-001", seed.Msc.Id, seed.Customer.Id, expiry ?? new DateOnly(2026, 10, 31), "MSC ANNA 042W");
        order.AddLine(ContainerCategory.Dry, dryQuantity);
        dbContext.DeliveryOrders.Add(order);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return order;
    }

    // Puts a container in the yard through the real gate-in handler
    private static async Task GateInAsync(
        AppDbContext dbContext, Seed seed, Container container, int bay, int tier = 1,
        LineOperator? lineOperator = null, ContainerCondition condition = ContainerCondition.Normal)
    {
        var result = await new GateInCommandHandler(dbContext, new FixedTimeProvider(Now)).HandleAsync(
            new GateInCommand(container.Id, (lineOperator ?? seed.Msc).Id, seed.Yard.Id, bay, 1, tier,
                ContainerGrade.A, condition, condition == ContainerCondition.Damaged ? "Dent on door" : null, "51C-11111"),
            TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
    }

    private static Task<Result<GateOutResponse>> GateOutAsync(
        AppDbContext dbContext, Container container, DeliveryOrder order, DateTimeOffset? now = null) =>
        new GateOutCommandHandler(dbContext, new FixedTimeProvider(now ?? Now)).HandleAsync(
            new GateOutCommand(container.Id, order.Id, "51c-99999"), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_Release_Container_Under_Order()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1);

        var result = await GateOutAsync(dbContext, seed.Dry1, order);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderNumber.Should().Be("DO-001");
        result.Value.RemainingQuantity.Should().Be(0);
        result.Value.VehicleNo.Should().Be("51C-99999");
        result.Value.GateOutAt.Should().Be(Now);
        var visit = dbContext.ContainerVisits.Single();
        visit.IsInYard.Should().BeFalse();
        visit.DeliveryOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Should_Allow_Gate_In_Again_After_Gate_Out()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1);
        await GateOutAsync(dbContext, seed.Dry1, order);

        // A new lifecycle starts; the same slot is free again
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1);

        dbContext.ContainerVisits.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Quantity_Is_Used_Up()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed, dryQuantity: 1);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1);
        await GateInAsync(dbContext, seed, seed.Dry2, bay: 3);
        await GateOutAsync(dbContext, seed.Dry1, order);

        var result = await GateOutAsync(dbContext, seed.Dry2, order);

        result.Error!.Code.Should().Be("DeliveryOrder.QuantityExceeded");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Order_Is_From_Another_Line_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1, lineOperator: seed.Cma);

        var result = await GateOutAsync(dbContext, seed.Dry1, order);

        result.Error!.Code.Should().Be("ContainerVisit.WrongLineOperator");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Order_Has_No_Line_For_The_Type()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Reefer, bay: 1);

        var result = await GateOutAsync(dbContext, seed.Reefer, order);

        result.Error!.Code.Should().Be("DeliveryOrder.TypeNotAllowed");
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Order_Expired_In_Vietnam_Time()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed, expiry: new DateOnly(2026, 10, 21));
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1);

        // 21/10 18:00 UTC is already 22/10 01:00 in Vietnam, after the expiry date
        var result = await GateOutAsync(dbContext, seed.Dry1, order, new DateTimeOffset(2026, 10, 21, 18, 0, 0, TimeSpan.Zero));

        result.Error!.Code.Should().Be("DeliveryOrder.Expired");
    }

    [Fact]
    public async Task Should_Not_Release_Damaged_Container_Until_Repaired()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 1, condition: ContainerCondition.Damaged);

        var damaged = await GateOutAsync(dbContext, seed.Dry1, order);
        await new UpdateContainerConditionCommandHandler(dbContext).HandleAsync(
            new UpdateContainerConditionCommand(seed.Dry1.Id, ContainerCondition.Normal, null),
            TestContext.Current.CancellationToken);
        var repaired = await GateOutAsync(dbContext, seed.Dry1, order);

        damaged.Error!.Code.Should().Be("ContainerVisit.ContainerDamaged");
        repaired.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_Conflict_When_A_Container_Is_Stacked_On_It()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed, dryQuantity: 2);
        await GateInAsync(dbContext, seed, seed.Dry1, bay: 5, tier: 1);
        await GateInAsync(dbContext, seed, seed.Dry2, bay: 5, tier: 2);

        var bottom = await GateOutAsync(dbContext, seed.Dry1, order);
        var top = await GateOutAsync(dbContext, seed.Dry2, order);
        var bottomAfter = await GateOutAsync(dbContext, seed.Dry1, order);

        bottom.Error!.Code.Should().Be("ContainerVisit.ContainerAbove");
        top.IsSuccess.Should().BeTrue();
        bottomAfter.IsSuccess.Should().BeTrue(); // nothing on it any more
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Not_In_Yard_Or_No_Order()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var order = await AddOrderAsync(dbContext, seed);
        await GateInAsync(dbContext, seed, seed.Dry2, bay: 1);

        var notInYard = await GateOutAsync(dbContext, seed.Dry1, order);
        var noOrder = await new GateOutCommandHandler(dbContext, new FixedTimeProvider(Now)).HandleAsync(
            new GateOutCommand(seed.Dry2.Id, Guid.NewGuid(), "51C-99999"), TestContext.Current.CancellationToken);

        notInYard.Error!.Code.Should().Be("ContainerVisit.NotInYard");
        noOrder.Error!.Code.Should().Be("DeliveryOrder.NotFound");
    }

    [Fact]
    public void Validator_Should_Require_Ids_And_Vehicle()
    {
        var validator = new GateOutValidator();
        var valid = new GateOutCommand(Guid.NewGuid(), Guid.NewGuid(), "51C-99999");

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { VehicleNo = "" }).IsValid.Should().BeFalse();
        validator.Validate(valid with { DeliveryOrderId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
