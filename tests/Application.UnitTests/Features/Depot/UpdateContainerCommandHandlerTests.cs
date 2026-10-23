using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class UpdateContainerCommandHandlerTests
{
    private static Container NewContainer() =>
        new("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

    // Fixed id of the owner partner seeded by SeedContainerAsync
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static UpdateContainerCommand Command(
        Guid id, ContainerSize size = ContainerSize.Ft20, ContainerCategory category = ContainerCategory.Dry,
        Guid? ownerId = null) =>
        new(id, "22G0", size, category, 30480, 2300, new DateOnly(2021, 1, 1), ownerId ?? OwnerId);

    private static async Task<Container> SeedContainerAsync(AppDbContext dbContext)
    {
        var container = NewContainer();
        dbContext.AddRange(container, new LineOperator("MSC", "MSC", isOwner: true) { Id = OwnerId });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return container;
    }

    [Fact]
    public async Task HandleAsync_Should_Update_Details()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);

        var result = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(container.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        container.IsoCode.Should().Be("22G0");
        container.TareWeightKg.Should().Be(2300);
        container.ContainerNumber.Should().Be("MSCU1234566");
        container.OwnerId.Should().Be(OwnerId);
    }

    [Fact]
    public async Task HandleAsync_Should_Reconcile_The_Owner_And_Keep_The_Old_Text()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = await SeedContainerAsync(dbContext);
        var hmm = new LineOperator("HMM", "Hyundai Merchant Marine", isOwner: true);
        var operatorOnly = new LineOperator("ONE", "Ocean Network Express", isOwner: false);
        dbContext.AddRange(hmm, operatorOnly);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notOwner = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(container.Id, ownerId: operatorOnly.Id), TestContext.Current.CancellationToken);
        var reconciled = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(container.Id, ownerId: hmm.Id), TestContext.Current.CancellationToken);

        notOwner.Error!.Code.Should().Be("Partner.NotOwner");
        reconciled.IsSuccess.Should().BeTrue();
        container.OwnerId.Should().Be(hmm.Id);
        container.OwnerName.Should().Be("MSC"); // the text typed before Đợt 3 is kept as it was
    }

    [Fact]
    public void Validator_Should_Reject_Type_Not_Matching_Iso_Code()
    {
        // The type is checked against the ISO code: 22G0 is group G (Dry), not Reefer
        var command = Command(Guid.NewGuid(), category: ContainerCategory.Reefer);

        var result = new UpdateContainerValidator().Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "IsoCode");
    }

    [Fact]
    public async Task HandleAsync_Should_Reject_Size_Change_While_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = NewContainer();
        var lineOperator = new LineOperator("MSC", "MSC", isOwner: true) { Id = OwnerId };
        var block = Block.CreatePhysical("A", 10, 4, 3);
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 1, 1, 1, ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
        dbContext.AddRange(container, lineOperator, block, visit);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(container.Id, ContainerSize.Ft40), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Container.SizeChangeInYard");
        container.Size.Should().Be(ContainerSize.Ft20);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_NotFound_When_Missing()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_Should_Reject_Tare_Above_Max_Gross()
    {
        var command = Command(Guid.NewGuid()) with { TareWeightKg = 40000 };

        new UpdateContainerValidator().Validate(command).IsValid.Should().BeFalse();
    }
}
