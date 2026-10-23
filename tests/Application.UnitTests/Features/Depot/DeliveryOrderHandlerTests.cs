using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Create;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.Get;
using TechSpherex.CleanArchitecture.Application.Features.DeliveryOrders.GetAll;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class DeliveryOrderHandlerTests
{
    // 21/10/2026 09:00 in Vietnam
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(7));
    private static readonly DateOnly Today = new(2026, 10, 21);

    private sealed record Seed(LineOperator LineOperator, Customer Customer);

    private static async Task<Seed> SeedAsync(AppDbContext dbContext)
    {
        var seed = new Seed(new LineOperator("MSC", "Mediterranean Shipping Company"), new Customer("0301234567", "Customer A"));
        dbContext.AddRange(seed.LineOperator, seed.Customer);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return seed;
    }

    private static CreateDeliveryOrderCommand Command(Seed seed, string number = "do-001", DateOnly? expiry = null) =>
        new(number, seed.LineOperator.Id, seed.Customer.Id, expiry ?? Today.AddDays(10), "MSC Anna 042W",
            [new DeliveryOrderLineRequest(ContainerCategory.Dry, 3, ContainerSize.Ft20), new DeliveryOrderLineRequest(ContainerCategory.Reefer, 1, ContainerSize.Ft40)]);

    private static Task<Result<CreateDeliveryOrderResponse>> CreateAsync(
        AppDbContext dbContext, CreateDeliveryOrderCommand command, DateTimeOffset? now = null) =>
        new CreateDeliveryOrderCommandHandler(dbContext, new FixedTimeProvider(now ?? Now))
            .HandleAsync(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Create_Should_Save_Order_With_Lines()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var result = await CreateAsync(dbContext, Command(seed));

        result.Value!.OrderNumber.Should().Be("DO-001");
        var order = dbContext.DeliveryOrders.Single();
        order.VesselVoyage.Should().Be("MSC ANNA 042W");
        dbContext.Set<DeliveryOrderLine>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Should_Accept_Expiry_Today_But_Not_Yesterday()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var today = await CreateAsync(dbContext, Command(seed, "DO-001", Today));
        var yesterday = await CreateAsync(dbContext, Command(seed, "DO-002", Today.AddDays(-1)));

        today.IsSuccess.Should().BeTrue();
        yesterday.Error!.Code.Should().Be("DeliveryOrder.AlreadyExpired");
    }

    [Fact]
    public async Task Create_Should_Use_Vietnam_Date()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        // 20/10 18:00 UTC is already 21/10 01:00 in Vietnam, so an order expiring on 20/10 is expired
        var utcEvening = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero);
        var result = await CreateAsync(dbContext, Command(seed, expiry: new DateOnly(2026, 10, 20)), utcEvening);

        result.Error!.Code.Should().Be("DeliveryOrder.AlreadyExpired");
    }

    [Fact]
    public async Task Create_Should_Return_Conflict_When_Number_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        await CreateAsync(dbContext, Command(seed, "DO-001"));

        var result = await CreateAsync(dbContext, Command(seed, " do-001 "));

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_Should_Return_NotFound_When_Customer_Or_Line_Operator_Missing()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);

        var noCustomer = await CreateAsync(dbContext, Command(seed) with { CustomerId = Guid.NewGuid() });
        var noOperator = await CreateAsync(dbContext, Command(seed) with { LineOperatorId = Guid.NewGuid() });

        noCustomer.Error!.Code.Should().Be("Customer.NotFound");
        noOperator.Error!.Code.Should().Be("LineOperator.NotFound");
    }

    [Fact]
    public void Validator_Should_Check_Number_And_Lines()
    {
        var validator = new CreateDeliveryOrderValidator();
        var valid = new CreateDeliveryOrderCommand("DO/2026-001", Guid.NewGuid(), Guid.NewGuid(), Today, "MSC ANNA 042W",
            [new DeliveryOrderLineRequest(ContainerCategory.Dry, 2, ContainerSize.Ft20)]);

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { OrderNumber = "DO 001" }).IsValid.Should().BeFalse(); // space not allowed
        validator.Validate(valid with { Lines = [] }).IsValid.Should().BeFalse();
        validator.Validate(valid with { Lines = [new(ContainerCategory.Dry, 0, ContainerSize.Ft20)] }).IsValid.Should().BeFalse();
        validator.Validate(valid with { Lines = [new(ContainerCategory.Dry, 1)] }).IsValid.Should().BeFalse(); // size is required (Q1)
        validator.Validate(valid with
        {
            Lines = [new(ContainerCategory.Dry, 1, ContainerSize.Ft20), new(ContainerCategory.Dry, 2, ContainerSize.Ft20)] // same type and size twice
        }).IsValid.Should().BeFalse();
        validator.Validate(valid with
        {
            Lines = [new(ContainerCategory.Dry, 1, ContainerSize.Ft20), new(ContainerCategory.Dry, 2, ContainerSize.Ft40)] // one line per size
        }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Get_Should_Return_Lines_With_Remaining_Quantity()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var created = await CreateAsync(dbContext, Command(seed));

        var result = await new GetDeliveryOrderQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetDeliveryOrderQuery(created.Value!.Id), TestContext.Current.CancellationToken);

        result.Value!.CustomerTaxCode.Should().Be("0301234567");
        result.Value.LineOperatorCode.Should().Be("MSC");
        result.Value.IsExpired.Should().BeFalse();
        result.Value.Lines.Should().ContainSingle(l =>
            l.Category == ContainerCategory.Dry && l.Size == ContainerSize.Ft20 && l.Quantity == 3 && l.ReleasedQuantity == 0 && l.RemainingQuantity == 3);
        result.Value.Status.Should().Be(DeliveryOrderStatus.NotStarted);
    }

    [Fact]
    public async Task Get_Should_Mark_Order_Expired_After_Expiry_Date()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        var created = await CreateAsync(dbContext, Command(seed, expiry: Today));
        var tomorrow = Now.AddDays(1);

        var result = await new GetDeliveryOrderQueryHandler(dbContext, new FixedTimeProvider(tomorrow))
            .HandleAsync(new GetDeliveryOrderQuery(created.Value!.Id), TestContext.Current.CancellationToken);
        var missing = await new GetDeliveryOrderQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetDeliveryOrderQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Value!.IsExpired.Should().BeTrue();
        missing.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_Page_Orders()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var seed = await SeedAsync(dbContext);
        foreach (var number in new[] { "DO-001", "DO-002", "DO-003" })
            await CreateAsync(dbContext, Command(seed, number));

        var result = await new GetAllDeliveryOrdersQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetAllDeliveryOrdersQuery(Page: 1, PageSize: 2), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
    }
}
