using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class UpdateContainerCommandHandlerTests
{
    private static Container NewContainer() =>
        new("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

    private static UpdateContainerCommand Command(Guid id, ContainerSize size = ContainerSize.Ft20) =>
        new(id, "22R1", size, ContainerCategory.Reefer, 30480, 3000, new DateOnly(2021, 1, 1), "MSC Line");

    [Fact]
    public async Task HandleAsync_Should_Update_Details()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = NewContainer();
        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new UpdateContainerCommandHandler(dbContext)
            .HandleAsync(Command(container.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        container.IsoCode.Should().Be("22R1");
        container.Category.Should().Be(ContainerCategory.Reefer);
        container.ContainerNumber.Should().Be("MSCU1234566");
    }

    [Fact]
    public async Task HandleAsync_Should_Reject_Size_Change_While_In_Yard()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = NewContainer();
        var lineOperator = new LineOperator("MSC", "MSC");
        var block = Block.CreatePhysical("A", 10, 4, 3);
        var visit = ContainerVisit.GateIn(container, lineOperator.Id, block, 1, 1, 1, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow).Value!;
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
