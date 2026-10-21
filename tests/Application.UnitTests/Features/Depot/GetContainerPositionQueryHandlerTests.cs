using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.GetPosition;
using TechSpherex.CleanArchitecture.Application.Features.ContainerVisits.Shared;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class GetContainerPositionQueryHandlerTests
{
    private static async Task<Container> SeedInYardAsync(AppDbContext dbContext, Block block, int? bay, int? row, int? tier)
    {
        var lineOperator = new LineOperator("MSC", "Mediterranean Shipping Company");
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, bay, row, tier, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;

        dbContext.AddRange(lineOperator, block, container, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return container;
    }

    private static Task<Result<ContainerPositionResponse>> GetAsync(
        AppDbContext dbContext, string number) =>
        new GetContainerPositionQueryHandler(dbContext).HandleAsync(
            new GetContainerPositionQuery(number), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Should_Return_Position_In_Physical_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedInYardAsync(dbContext, Block.CreatePhysical("A", 10, 4, 3), 5, 2, 1);

        // Number is normalized like at creation (trim, upper case)
        var result = await GetAsync(dbContext, " mscu1234566 ");

        result.IsSuccess.Should().BeTrue();
        result.Value!.BlockCode.Should().Be("A");
        result.Value.IsVirtualBlock.Should().BeFalse();
        result.Value.Bay.Should().Be(5);
        result.Value.Row.Should().Be(2);
        result.Value.Tier.Should().Be(1);
        result.Value.LineOperatorCode.Should().Be("MSC");
    }

    [Fact]
    public async Task Should_Return_Virtual_Block_Without_Bay_Row_Tier()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedInYardAsync(dbContext, Block.CreateVirtual("WAIT"), null, null, null);

        var result = await GetAsync(dbContext, "MSCU1234566");

        result.Value!.IsVirtualBlock.Should().BeTrue();
        result.Value.Bay.Should().BeNull();
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Unknown_Or_Not_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        dbContext.Containers.Add(new Container("CMAU1234564", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3800, null, "CMA"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var unknown = await GetAsync(dbContext, "MSCU1234566");
        var notInYard = await GetAsync(dbContext, "CMAU1234564");

        unknown.Error!.Code.Should().Be("Container.NotFound");
        notInYard.Error!.Code.Should().Be("ContainerVisit.NotInYard");
    }
}
