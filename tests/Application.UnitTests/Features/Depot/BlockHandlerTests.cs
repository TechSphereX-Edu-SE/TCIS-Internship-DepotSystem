using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class BlockHandlerTests
{
    // Seeds a physical block with one 20ft container at bay 5, row 3, tier 2
    private static async Task<Block> SeedBlockWithContainerAsync(AppDbContext dbContext)
    {
        var lineOperator = new LineOperator("MSC", "MSC");
        var block = Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3);
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 5, 3, 2, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;

        dbContext.AddRange(lineOperator, block, container, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return block;
    }

    [Fact]
    public async Task Create_Should_Create_Physical_And_Virtual_Blocks()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateBlockCommandHandler(dbContext);

        var physical = await handler.HandleAsync(new CreateBlockCommand("c", null, false, 10, 4, 4), TestContext.Current.CancellationToken);
        var virtualBlock = await handler.HandleAsync(new CreateBlockCommand("REPAIR", "Repair area", true, null, null, null), TestContext.Current.CancellationToken);

        physical.Value!.Code.Should().Be("C");
        virtualBlock.Value!.IsVirtual.Should().BeTrue();
        dbContext.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Should_Return_Conflict_When_Code_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateBlockCommandHandler(dbContext);
        await handler.HandleAsync(new CreateBlockCommand("A", null, false, 10, 4, 4), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(new CreateBlockCommand("a", null, true, null, null, null), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Create_Validator_Should_Enforce_Dimensions_By_Block_Type()
    {
        var validator = new CreateBlockValidator();

        validator.Validate(new CreateBlockCommand("A", null, false, 10, 4, 4)).IsValid.Should().BeTrue();
        validator.Validate(new CreateBlockCommand("A", null, false, 10, null, 4)).IsValid.Should().BeFalse(); // physical, missing rows
        validator.Validate(new CreateBlockCommand("A", null, false, 0, 4, 4)).IsValid.Should().BeFalse();     // physical, 0 bays
        validator.Validate(new CreateBlockCommand("A", null, false, 10, 4, 1)).IsValid.Should().BeFalse();    // physical, 1 tier
        validator.Validate(new CreateBlockCommand("V", null, true, null, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new CreateBlockCommand("V", null, true, 10, null, null)).IsValid.Should().BeFalse(); // virtual with bays
    }

    [Fact]
    public async Task Get_Should_Count_Containers_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);

        var result = await new GetBlockQueryHandler(dbContext).HandleAsync(new GetBlockQuery(block.Id), TestContext.Current.CancellationToken);

        result.Value!.ContainersInYard.Should().Be(1);
    }

    [Fact]
    public async Task Resize_Should_Expand_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);

        var result = await new ResizeBlockCommandHandler(dbContext)
            .HandleAsync(new ResizeBlockCommand(block.Id, 20, 6, 5), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        block.MaxBays.Should().Be(20);
    }

    [Theory]
    [InlineData(4, 4, 3)]  // container at bay 5
    [InlineData(10, 2, 3)] // container at row 3
    [InlineData(10, 4, 1)] // container at tier 2
    public async Task Resize_Should_Reject_Shrink_When_Container_Would_Be_Outside(int bays, int rows, int tiers)
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);

        var result = await new ResizeBlockCommandHandler(dbContext)
            .HandleAsync(new ResizeBlockCommand(block.Id, bays, rows, tiers), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Block.ContainersOutsideNewSize");
        block.MaxBays.Should().Be(10);
    }

    [Fact]
    public async Task Resize_Should_Reject_Virtual_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = Block.CreateVirtual("WAIT");
        dbContext.Blocks.Add(block);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new ResizeBlockCommandHandler(dbContext)
            .HandleAsync(new ResizeBlockCommand(block.Id, 10, 4, 4), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Block.VirtualCannotResize");
    }

    [Fact]
    public async Task Resize_Should_Reject_Shrink_That_Cuts_A_40ft_Footprint()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var lineOperator = new LineOperator("CMA", "CMA");
        var block = Block.CreatePhysical("B", maxBays: 10, maxRows: 4, maxTiers: 3);
        var container = new Container("CMAU7654327", "42G1", ContainerSize.Ft40, ContainerCategory.Dry, 30480, 3750, null, "CMA");
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 6, 1, 1, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
        dbContext.AddRange(lineOperator, block, container, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new ResizeBlockCommandHandler(dbContext);

        // 40ft in bay 6 covers bays 5 and 7, so 6 bays is too small but 7 is enough
        var tooSmall = await handler.HandleAsync(new ResizeBlockCommand(block.Id, 6, 4, 3), TestContext.Current.CancellationToken);
        var enough = await handler.HandleAsync(new ResizeBlockCommand(block.Id, 7, 4, 3), TestContext.Current.CancellationToken);

        tooSmall.Error!.Code.Should().Be("Block.ContainersOutsideNewSize");
        enough.IsSuccess.Should().BeTrue();
    }


    [Fact]
    public async Task GetAll_Should_List_Blocks_By_Code_With_Containers_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        await SeedBlockWithContainerAsync(dbContext); // block A with one container
        dbContext.Blocks.Add(Block.CreateVirtual("WAIT"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetAllBlocksQueryHandler(dbContext)
            .HandleAsync(new GetAllBlocksQuery(), TestContext.Current.CancellationToken);

        result.Value!.Items.Select(b => b.Code).Should().Equal("A", "WAIT");
        result.Value.Items[0].ContainersInYard.Should().Be(1);
        result.Value.Items[1].ContainersInYard.Should().Be(0);
    }

    [Fact]
    public void Resize_Validator_Should_Require_Positive_Size_And_Two_Tiers()
    {
        var validator = new ResizeBlockValidator();

        validator.Validate(new ResizeBlockCommand(Guid.NewGuid(), 10, 4, 2)).IsValid.Should().BeTrue();
        validator.Validate(new ResizeBlockCommand(Guid.NewGuid(), 10, 4, 1)).IsValid.Should().BeFalse(); // 1 tier
        validator.Validate(new ResizeBlockCommand(Guid.NewGuid(), 0, 4, 3)).IsValid.Should().BeFalse();  // 0 bays
        validator.Validate(new ResizeBlockCommand(Guid.Empty, 10, 4, 3)).IsValid.Should().BeFalse();
    }
}
