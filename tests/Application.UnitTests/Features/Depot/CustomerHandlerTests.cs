using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Create;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Get;
using TechSpherex.CleanArchitecture.Application.Features.Customers.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.Customers.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class CustomerHandlerTests
{
    [Fact]
    public async Task Create_Should_Return_Conflict_When_Tax_Code_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var handler = new CreateCustomerCommandHandler(dbContext);
        (await handler.HandleAsync(new CreateCustomerCommand("0301234567", "Customer A"),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        var result = await handler.HandleAsync(new CreateCustomerCommand(" 0301234567 ", "Customer B"),
            TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Theory]
    [InlineData("0301234567", true)]
    [InlineData("0301234567-001", true)]
    [InlineData("030123456", false)]      // 9 digits
    [InlineData("0301234567-01", false)]  // branch suffix must be 3 digits
    [InlineData("03012345AB", false)]
    public void Create_Validator_Should_Check_Tax_Code_Format(string taxCode, bool expected)
    {
        new CreateCustomerValidator().Validate(new CreateCustomerCommand(taxCode, "Name"))
            .IsValid.Should().Be(expected);
    }

    [Fact]
    public async Task GetAll_Should_Search_By_Tax_Code_Or_Name()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var create = new CreateCustomerCommandHandler(dbContext);
        await create.HandleAsync(new CreateCustomerCommand("0301234567", "Minh An Import Export"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateCustomerCommand("0309876543", "Phuong Nam Logistics"), TestContext.Current.CancellationToken);
        var handler = new GetAllCustomersQueryHandler(dbContext);

        var byName = await handler.HandleAsync(new GetAllCustomersQuery("logistics"), TestContext.Current.CancellationToken);
        var byTaxCode = await handler.HandleAsync(new GetAllCustomersQuery("0301"), TestContext.Current.CancellationToken);
        var all = await handler.HandleAsync(new GetAllCustomersQuery(), TestContext.Current.CancellationToken);

        byName.Value!.Items.Should().ContainSingle(c => c.TaxCode == "0309876543");
        byTaxCode.Value!.Items.Should().ContainSingle(c => c.Name == "Minh An Import Export");
        all.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Update_Should_Return_NotFound_When_Missing()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new UpdateCustomerCommandHandler(dbContext)
            .HandleAsync(new UpdateCustomerCommand(Guid.NewGuid(), "X"), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }


    [Fact]
    public async Task Get_And_Update_Should_Rename_Customer()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var created = await new CreateCustomerCommandHandler(dbContext)
            .HandleAsync(new CreateCustomerCommand("0301234567", "Old Name"), TestContext.Current.CancellationToken);
        var id = created.Value!.Id;

        var update = await new UpdateCustomerCommandHandler(dbContext)
            .HandleAsync(new UpdateCustomerCommand(id, " New Name "), TestContext.Current.CancellationToken);
        var get = await new GetCustomerQueryHandler(dbContext)
            .HandleAsync(new GetCustomerQuery(id), TestContext.Current.CancellationToken);
        var missing = await new GetCustomerQueryHandler(dbContext)
            .HandleAsync(new GetCustomerQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        update.IsSuccess.Should().BeTrue();
        get.Value!.Name.Should().Be("New Name");
        get.Value.TaxCode.Should().Be("0301234567"); // tax code never changes
        missing.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Update_Validator_Should_Require_Name()
    {
        var validator = new UpdateCustomerValidator();

        validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "Customer A")).IsValid.Should().BeTrue();
        validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), new string('x', 201))).IsValid.Should().BeFalse();
        validator.Validate(new UpdateCustomerCommand(Guid.Empty, "Customer A")).IsValid.Should().BeFalse();
    }
}
