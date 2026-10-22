using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class LineOperatorHandlerTests
{
    [Fact]
    public async Task Create_Should_Store_Uppercase_Code()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateLineOperatorCommandHandler(dbContext);

        var result = await handler.HandleAsync(new CreateLineOperatorCommand(" msc ", "Mediterranean Shipping Company"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("MSC");
        dbContext.LineOperators.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_Should_Return_Conflict_When_Code_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateLineOperatorCommandHandler(dbContext);
        await handler.HandleAsync(new CreateLineOperatorCommand("MSC", "MSC"), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(new CreateLineOperatorCommand("msc", "Other"),
            TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Create_Validator_Should_Reject_Invalid_Code()
    {
        var validator = new CreateLineOperatorValidator();

        validator.Validate(new CreateLineOperatorCommand("M", "Name")).IsValid.Should().BeFalse();
        validator.Validate(new CreateLineOperatorCommand("MS-C", "Name")).IsValid.Should().BeFalse();
        validator.Validate(new CreateLineOperatorCommand("MSC", "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_Should_Rename_And_Get_Should_Return_New_Name()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var created = await new CreateLineOperatorCommandHandler(dbContext)
            .HandleAsync(new CreateLineOperatorCommand("CMA", "CMA"), TestContext.Current.CancellationToken);

        var update = await new UpdateLineOperatorCommandHandler(dbContext)
            .HandleAsync(new UpdateLineOperatorCommand(created.Value!.Id, "CMA CGM"), TestContext.Current.CancellationToken);
        var get = await new GetLineOperatorQueryHandler(dbContext)
            .HandleAsync(new GetLineOperatorQuery(created.Value.Id), TestContext.Current.CancellationToken);

        update.IsSuccess.Should().BeTrue();
        get.Value!.Name.Should().Be("CMA CGM");
    }

    [Fact]
    public async Task Update_Should_Return_NotFound_When_Missing()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new UpdateLineOperatorCommandHandler(dbContext)
            .HandleAsync(new UpdateLineOperatorCommand(Guid.NewGuid(), "X"), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }


    [Fact]
    public async Task GetAll_Should_List_Line_Operators_By_Code()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var create = new CreateLineOperatorCommandHandler(dbContext);
        await create.HandleAsync(new CreateLineOperatorCommand("MSC", "Mediterranean Shipping Company"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("CMA", "CMA CGM"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("HMM", "Hyundai Merchant Marine"), TestContext.Current.CancellationToken);

        var result = await new GetAllLineOperatorsQueryHandler(dbContext)
            .HandleAsync(new GetAllLineOperatorsQuery(Page: 1, PageSize: 2), TestContext.Current.CancellationToken);

        result.Value!.Items.Select(l => l.Code).Should().Equal("CMA", "HMM");
        result.Value.TotalCount.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Update_Validator_Should_Require_Name()
    {
        var validator = new UpdateLineOperatorValidator();

        validator.Validate(new UpdateLineOperatorCommand(Guid.NewGuid(), "CMA CGM")).IsValid.Should().BeTrue();
        validator.Validate(new UpdateLineOperatorCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateLineOperatorCommand(Guid.Empty, "CMA CGM")).IsValid.Should().BeFalse();
    }
}
