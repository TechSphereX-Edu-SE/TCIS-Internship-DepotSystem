using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Create;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Delete;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Get;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Resize;
using TechSpherex.CleanArchitecture.Application.Features.Blocks.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class BlockHandlerTests
{
    private static readonly IReadOnlyList<ContainerCategory> AllTypes = Block.AllCategories;

    // Seeds a physical block with one 20ft container at bay 5, row 3, tier 2
    private static async Task<Block> SeedBlockWithContainerAsync(AppDbContext dbContext)
    {
        var lineOperator = new LineOperator("MSC", "MSC");
        var block = Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3);
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 5, 3, 2, ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;

        dbContext.AddRange(lineOperator, block, container, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return block;
    }

    [Fact]
    public async Task Create_Should_Create_Physical_And_Virtual_Blocks()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateBlockCommandHandler(dbContext);

        var physical = await handler.HandleAsync(new CreateBlockCommand("c", null, false, 10, 4, 4, AllTypes), TestContext.Current.CancellationToken);
        var virtualBlock = await handler.HandleAsync(new CreateBlockCommand("REPAIR", "Repair area", true, null, null, null, AllTypes), TestContext.Current.CancellationToken);

        physical.Value!.Code.Should().Be("C");
        virtualBlock.Value!.IsVirtual.Should().BeTrue();
        dbContext.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Should_Return_Conflict_When_Code_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateBlockCommandHandler(dbContext);
        await handler.HandleAsync(new CreateBlockCommand("A", null, false, 10, 4, 4, AllTypes), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(new CreateBlockCommand("a", null, true, null, null, null, AllTypes), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Create_Validator_Should_Enforce_Dimensions_By_Block_Type()
    {
        var validator = new CreateBlockValidator();

        validator.Validate(new CreateBlockCommand("A", null, false, 10, 4, 4, AllTypes)).IsValid.Should().BeTrue();
        validator.Validate(new CreateBlockCommand("A", null, false, 10, null, 4, AllTypes)).IsValid.Should().BeFalse(); // physical, missing rows
        validator.Validate(new CreateBlockCommand("A", null, false, 0, 4, 4, AllTypes)).IsValid.Should().BeFalse();     // physical, 0 bays
        validator.Validate(new CreateBlockCommand("A", null, false, 10, 4, 1, AllTypes)).IsValid.Should().BeFalse();    // physical, 1 tier
        validator.Validate(new CreateBlockCommand("V", null, true, null, null, null, AllTypes)).IsValid.Should().BeTrue();
        validator.Validate(new CreateBlockCommand("V", null, true, 10, null, null, AllTypes)).IsValid.Should().BeFalse(); // virtual with bays
        validator.Validate(new CreateBlockCommand("V", null, true, null, null, null, [])).IsValid.Should().BeFalse();     // no container type
        validator.Validate(new CreateBlockCommand("V", null, true, null, null, null, [(ContainerCategory)99])).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Should_Store_Allowed_Types()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateBlockCommandHandler(dbContext);

        var created = await handler.HandleAsync(
            new CreateBlockCommand("B", null, false, 10, 4, 4, [ContainerCategory.Reefer, ContainerCategory.Reefer]),
            TestContext.Current.CancellationToken);

        var block = await new GetBlockQueryHandler(dbContext).HandleAsync(new GetBlockQuery(created.Value!.Id), TestContext.Current.CancellationToken);
        block.Value!.AllowedCategories.Should().Equal(ContainerCategory.Reefer);
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
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 6, 1, 1, ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
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

    // ---------- update (feedback 9) ----------

    private static Task<Result> UpdateAsync(AppDbContext dbContext, Block block, string code, params ContainerCategory[] categories) =>
        new UpdateBlockCommandHandler(dbContext).HandleAsync(
            new UpdateBlockCommand(block.Id, code, "  Khu mới  ", categories), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Update_Should_Change_Code_Description_And_Types_And_Keep_Visits()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext); // block A with a Dry container

        var result = await UpdateAsync(dbContext, block, "a2", ContainerCategory.Dry, ContainerCategory.Reefer);

        result.IsSuccess.Should().BeTrue();
        block.Code.Should().Be("A2");
        block.Description.Should().Be("Khu mới");
        block.AllowedCategories.Should().Equal(ContainerCategory.Dry, ContainerCategory.Reefer);
        // Same Id, so the visit follows the new code
        dbContext.ContainerVisits.Single().BlockId.Should().Be(block.Id);
    }

    [Fact]
    public async Task Update_Should_Reject_A_Code_Used_By_Another_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);
        dbContext.Blocks.Add(Block.CreateVirtual("WAIT"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var taken = await UpdateAsync(dbContext, block, "wait", ContainerCategory.Dry);
        var own = await UpdateAsync(dbContext, block, "A", ContainerCategory.Dry); // its own code is fine

        taken.Error!.Code.Should().Be("Block.Duplicate");
        own.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Should_Not_Remove_A_Type_Still_In_The_Block()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext); // a Dry container is in the block

        var result = await UpdateAsync(dbContext, block, "A", ContainerCategory.Reefer);

        result.Error!.Code.Should().Be("Block.ContainersOfRemovedType");
        block.AllowedCategories.Should().Contain(ContainerCategory.Dry);
    }

    [Fact]
    public void Update_Validator_Should_Require_Code_And_Types()
    {
        var validator = new UpdateBlockValidator();
        var valid = new UpdateBlockCommand(Guid.NewGuid(), "B1", null, [ContainerCategory.Dry]);

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { Code = "B-1" }).IsValid.Should().BeFalse();
        validator.Validate(valid with { AllowedCategories = [] }).IsValid.Should().BeFalse();
        validator.Validate(valid with { Id = Guid.Empty }).IsValid.Should().BeFalse();
    }

    // ---------- delete / archive (feedback 9) ----------

    private static Task<Result<DeleteBlockResponse>> DeleteAsync(AppDbContext dbContext, Block block) =>
        new DeleteBlockCommandHandler(dbContext, new FixedTimeProvider(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.FromHours(7))))
            .HandleAsync(new DeleteBlockCommand(block.Id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Delete_Should_Remove_A_Block_Without_History()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = Block.CreateVirtual("TMP");
        dbContext.Blocks.Add(block);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DeleteAsync(dbContext, block);

        result.Value!.Archived.Should().BeFalse();
        dbContext.Blocks.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Should_Refuse_A_Block_With_Containers()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);

        var result = await DeleteAsync(dbContext, block);

        result.Error!.Code.Should().Be("Block.HasContainers");
        block.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Should_Archive_A_Block_With_History_And_Hide_It_From_New_Placements()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var block = await SeedBlockWithContainerAsync(dbContext);
        var visit = dbContext.ContainerVisits.Single();
        var customer = new Customer("0301234567", "Customer A");
        // The seeded visit came in "now" (real clock), so the order and the gate-out follow the real clock too
        var order = new DeliveryOrder("DO-1", visit.LineOperatorId, customer.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "VESSEL 01");
        order.AddLine(ContainerCategory.Dry, 1);
        visit.GateOut(order, ContainerCategory.Dry, "51C-99999", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        dbContext.AddRange(customer, order);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await DeleteAsync(dbContext, block);
        var active = await new GetAllBlocksQueryHandler(dbContext).HandleAsync(new GetAllBlocksQuery(), TestContext.Current.CancellationToken);
        var all = await new GetAllBlocksQueryHandler(dbContext).HandleAsync(new GetAllBlocksQuery(IncludeArchived: true), TestContext.Current.CancellationToken);

        result.Value!.Archived.Should().BeTrue();
        block.IsArchived.Should().BeTrue();
        active.Value!.Items.Should().BeEmpty();
        all.Value!.Items.Single().ArchivedAt.Should().NotBeNull();
        block.CheckAccepts(ContainerCategory.Dry).Error!.Code.Should().Be("Block.Archived");
        (await DeleteAsync(dbContext, block)).Error!.Code.Should().Be("Block.Archived");
        (await UpdateAsync(dbContext, block, "A", ContainerCategory.Dry)).Error!.Code.Should().Be("Block.Archived");
    }

    [Fact]
    public void Placement_Version_Should_Change_When_Containers_Enter_Or_Leave()
    {
        var lineOperator = new LineOperator("MSC", "MSC");
        var a = Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3);
        var wait = Block.CreateVirtual("WAIT");
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

        var visit = ContainerVisit.GateIn(container, lineOperator.Id, a, 1, 1, 1, ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
        a.PlacementVersion.Should().Be(1);

        // Only the target is known here (the old block navigation is not loaded outside EF)
        visit.MoveTo(wait, null, null, null, container.Size).IsSuccess.Should().BeTrue();
        wait.PlacementVersion.Should().Be(1);
    }
}
