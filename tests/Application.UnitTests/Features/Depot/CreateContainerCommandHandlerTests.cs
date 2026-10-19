using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Create;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class CreateContainerCommandHandlerTests
{
    private static CreateContainerCommand Command(string number = "CSQU3054383") =>
        new(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, new DateOnly(2020, 1, 1), "MSC");

    [Fact]
    public async Task HandleAsync_Should_Create_Container()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateContainerCommandHandler(dbContext);

        var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        dbContext.Containers.Should().ContainSingle(c => c.ContainerNumber == "CSQU3054383");
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Conflict_When_Number_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateContainerCommandHandler(dbContext);
        await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(Command(" csqu3054383 "), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Validator_Should_Reject_Wrong_Check_Digit()
    {
        var result = new CreateContainerValidator().Validate(Command("CSQU3054384"));

        result.IsValid.Should().BeFalse();
    }
}
