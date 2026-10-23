using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Delete;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Get;
using TechSpherex.CleanArchitecture.Application.Features.Containers.GetAll;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

// Get, list and delete of container master data
public sealed class ContainerQueryHandlerTests
{
    // 21/10/2026 09:00 in Vietnam: "today" for the current operator
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(7));

    private static async Task<Container> SeedContainerAsync(AppDbContext dbContext, string number = "MSCU1234566")
    {
        var container = new Container(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200,
            new DateOnly(2019, 5, 1), "MSC");
        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return container;
    }

    [Fact]
    public async Task Get_Should_Return_Container_Details()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);

        var result = await new GetContainerQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetContainerQuery(container.Id), TestContext.Current.CancellationToken);

        result.Value!.ContainerNumber.Should().Be("MSCU1234566");
        result.Value.Size.Should().Be("Ft20");
        result.Value.Category.Should().Be("Dry");
        result.Value.Condition.Should().Be("Normal");
        result.Value.ManufactureDate.Should().Be(new DateOnly(2019, 5, 1));
    }

    [Fact]
    public async Task Get_Should_Return_NotFound_For_Unknown_Id()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new GetContainerQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetContainerQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_Page_Containers()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedContainerAsync(dbContext, "MSCU1234566");
        await SeedContainerAsync(dbContext, "CMAU1234564");
        await SeedContainerAsync(dbContext, "CSQU3054383");

        var result = await new GetAllContainersQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetAllContainersQuery(Page: 2, PageSize: 2), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(3);
        result.Value.HasPreviousPage.Should().BeTrue();
        result.Value.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_Should_Search_By_Container_Number()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedContainerAsync(dbContext, "MSCU1234566");
        await SeedContainerAsync(dbContext, "MSCU5551234");
        await SeedContainerAsync(dbContext, "CMAU1234564");

        // Lower case and spaces are normalized like the stored numbers
        var result = await new GetAllContainersQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetAllContainersQuery(Search: " mscu "), TestContext.Current.CancellationToken);

        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().OnlyContain(c => c.ContainerNumber.StartsWith("MSCU"));
    }

    [Fact]
    public async Task GetAll_Should_Find_Serial_Digits_And_Sort_By_Number()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedContainerAsync(dbContext, "MSCU1234566");
        await SeedContainerAsync(dbContext, "CMAU1234564");
        await SeedContainerAsync(dbContext, "CSQU3054383");

        // Digits match inside the number (not a prefix), so both are in the "rest" group, by number
        var result = await new GetAllContainersQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetAllContainersQuery(Search: "1234"), TestContext.Current.CancellationToken);

        result.Value!.Items.Select(c => c.ContainerNumber).Should().Equal("CMAU1234564", "MSCU1234566");
    }

    [Fact]
    public async Task Get_Should_Return_The_Owner_And_Todays_Operator()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext, "HMMU2468109");
        var hmm = DepotTestData.Partner("HMM");
        var cma = DepotTestData.Partner("CMA");
        container.SetOwner(hmm);
        // HMM owns it, CMA rents it from 01/10/2026 (feedback 2 example)
        dbContext.AddRange(hmm, cma, DepotTestData.Confirmed(container, cma, from: new DateOnly(2026, 10, 1)));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetContainerQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetContainerQuery(container.Id), TestContext.Current.CancellationToken);

        result.Value!.OwnerCode.Should().Be("HMM");
        result.Value.CurrentOperatorCode.Should().Be("CMA");
    }

    [Fact]
    public async Task GetAll_Should_Filter_By_Todays_Operator_Not_By_Owner()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var rented = await SeedContainerAsync(dbContext, "HMMU2468109");
        var own = await SeedContainerAsync(dbContext, "HMMU3007776");
        var ended = await SeedContainerAsync(dbContext, "CMAU1234564");
        var hmm = DepotTestData.Partner("HMM");
        var cma = DepotTestData.Partner("CMA");
        rented.SetOwner(hmm);
        own.SetOwner(hmm);
        dbContext.AddRange(hmm, cma,
            DepotTestData.Confirmed(rented, cma),                                          // HMM owns, CMA operates
            DepotTestData.Confirmed(own, hmm),                                             // HMM owns and operates
            DepotTestData.Confirmed(ended, cma, to: new DateOnly(2026, 10, 20)));          // CMA's period ended yesterday
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetAllContainersQueryHandler(dbContext, new FixedTimeProvider(Now))
            .HandleAsync(new GetAllContainersQuery(OperatorId: cma.Id), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].ContainerNumber.Should().Be("HMMU2468109");
        result.Value.Items[0].OwnerCode.Should().Be("HMM");
        result.Value.Items[0].CurrentOperatorCode.Should().Be("CMA");
    }

    [Fact]
    public async Task Delete_Should_Keep_Container_With_Operating_History()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);
        var msc = DepotTestData.Partner("MSC");
        dbContext.AddRange(msc, DepotTestData.Confirmed(container, msc));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new DeleteContainerCommandHandler(dbContext)
            .HandleAsync(new DeleteContainerCommand(container.Id), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Container.HasHistory");
    }

    [Fact]
    public async Task Delete_Should_Remove_Container_Without_History()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);

        var result = await new DeleteContainerCommandHandler(dbContext)
            .HandleAsync(new DeleteContainerCommand(container.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        dbContext.Containers.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Should_Keep_Container_That_Has_Been_In_The_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);
        var lineOperator = new LineOperator("MSC", "MSC");
        var wait = Block.CreateVirtual("WAIT");
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, wait, null, null, null, ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
        dbContext.AddRange(lineOperator, wait, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new DeleteContainerCommandHandler(dbContext)
            .HandleAsync(new DeleteContainerCommand(container.Id), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Container.HasHistory");
        dbContext.Containers.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_Should_Return_NotFound_For_Unknown_Id()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new DeleteContainerCommandHandler(dbContext)
            .HandleAsync(new DeleteContainerCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
