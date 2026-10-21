using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.UpdateCondition;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class UpdateContainerConditionCommandHandlerTests
{
    [Fact]
    public async Task Should_Set_Damaged_With_Note_Then_Back_To_Normal()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");
        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new UpdateContainerConditionCommandHandler(dbContext);

        await handler.HandleAsync(new UpdateContainerConditionCommand(container.Id, ContainerCondition.Damaged, "Bent corner post"),
            TestContext.Current.CancellationToken);
        container.Condition.Should().Be(ContainerCondition.Damaged);
        container.ConditionNote.Should().Be("Bent corner post");

        // Back to Normal after repair: the damage note is cleared by the domain
        await handler.HandleAsync(new UpdateContainerConditionCommand(container.Id, ContainerCondition.Normal, "ignored"),
            TestContext.Current.CancellationToken);
        container.Condition.Should().Be(ContainerCondition.Normal);
        container.ConditionNote.Should().BeNull();
    }

    [Fact]
    public async Task Should_Return_NotFound_For_Unknown_Container()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new UpdateContainerConditionCommandHandler(dbContext).HandleAsync(
            new UpdateContainerConditionCommand(Guid.NewGuid(), ContainerCondition.Normal, null), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_Should_Reject_Unknown_Condition_And_Long_Note()
    {
        var validator = new UpdateContainerConditionValidator();
        var valid = new UpdateContainerConditionCommand(Guid.NewGuid(), ContainerCondition.Damaged, "Dent");

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { Condition = (ContainerCondition)9 }).IsValid.Should().BeFalse();
        validator.Validate(valid with { ConditionNote = new string('x', 501) }).IsValid.Should().BeFalse();
    }
}
