using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Create;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Get;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.GetAll;
using TechSpherex.CleanArchitecture.Application.Features.LineOperators.Update;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
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
    public async Task GetAll_Should_Rank_Exact_Code_Then_Prefix_Then_Other_Matches()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var create = new CreateLineOperatorCommandHandler(dbContext);
        await create.HandleAsync(new CreateLineOperatorCommand("XCMA", "Other line"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("CMAX", "Other line"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("ZIM", "Partner of CMA"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("CMA", "CMA CGM"), TestContext.Current.CancellationToken);
        await create.HandleAsync(new CreateLineOperatorCommand("MSC", "Mediterranean Shipping Company"), TestContext.Current.CancellationToken);

        var result = await new GetAllLineOperatorsQueryHandler(dbContext)
            .HandleAsync(new GetAllLineOperatorsQuery(Search: " cma "), TestContext.Current.CancellationToken);

        // Exact code, then code prefix, then the rest by code (inside the code or in the name)
        result.Value!.Items.Select(l => l.Code).Should().Equal("CMA", "CMAX", "XCMA", "ZIM");
        result.Value.TotalCount.Should().Be(4);
    }

    [Fact]
    public void Update_Validator_Should_Require_Name()
    {
        var validator = new UpdateLineOperatorValidator();

        validator.Validate(new UpdateLineOperatorCommand(Guid.NewGuid(), "CMA CGM")).IsValid.Should().BeTrue();
        validator.Validate(new UpdateLineOperatorCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateLineOperatorCommand(Guid.Empty, "CMA CGM")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Should_Store_Tax_Code_And_Roles()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var result = await new CreateLineOperatorCommandHandler(dbContext).HandleAsync(
            new CreateLineOperatorCommand("TRI", "Triton International", " 0312345678 ", IsOwner: true, IsOperator: false),
            TestContext.Current.CancellationToken);

        var partner = dbContext.LineOperators.Single(p => p.Id == result.Value!.Id);
        partner.TaxCode.Should().Be("0312345678");
        partner.IsOwner.Should().BeTrue();
        partner.IsOperator.Should().BeFalse();
    }

    [Fact]
    public void Validators_Should_Require_A_Role_And_A_Valid_Tax_Code()
    {
        var create = new CreateLineOperatorValidator();
        var update = new UpdateLineOperatorValidator();

        create.Validate(new CreateLineOperatorCommand("TRI", "Triton", "0312345678-001", true, false)).IsValid.Should().BeTrue();
        create.Validate(new CreateLineOperatorCommand("TRI", "Triton", "12345", true, false)).IsValid.Should().BeFalse();
        create.Validate(new CreateLineOperatorCommand("TRI", "Triton", null, false, false)).IsValid.Should().BeFalse();
        update.Validate(new UpdateLineOperatorCommand(Guid.NewGuid(), "Triton", null, false, false)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_Should_Filter_By_Role()
    {
        await using var dbContext = TestDbContextFactory.Create();
        dbContext.LineOperators.AddRange(
            new LineOperator("CMA", "CMA CGM", isOwner: true, isOperator: true),
            new LineOperator("ONE", "Ocean Network Express", isOwner: false, isOperator: true),
            new LineOperator("TRI", "Triton International", isOwner: true, isOperator: false));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new GetAllLineOperatorsQueryHandler(dbContext);

        var owners = await handler.HandleAsync(new GetAllLineOperatorsQuery(Role: PartnerRole.Owner), TestContext.Current.CancellationToken);
        var operators = await handler.HandleAsync(new GetAllLineOperatorsQuery(Role: PartnerRole.Operator), TestContext.Current.CancellationToken);

        owners.Value!.Items.Select(p => p.Code).Should().Equal("CMA", "TRI");
        operators.Value!.Items.Select(p => p.Code).Should().Equal("CMA", "ONE");
    }

    [Fact]
    public async Task Update_Should_Not_Remove_A_Role_In_Use()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var hmm = new LineOperator("HMM", "Hyundai Merchant Marine", isOwner: true, isOperator: true);
        var container = new Container("HMMU2468109", "45R1", ContainerSize.Ft40, ContainerCategory.Reefer, 34000, 4800, null, "HMM");
        container.SetOwner(hmm);
        dbContext.AddRange(hmm, container, DepotTestData.Confirmed(container, hmm));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new UpdateLineOperatorCommandHandler(dbContext);

        var dropOwner = await handler.HandleAsync(
            new UpdateLineOperatorCommand(hmm.Id, "HMM", null, IsOwner: false, IsOperator: true), TestContext.Current.CancellationToken);
        var dropOperator = await handler.HandleAsync(
            new UpdateLineOperatorCommand(hmm.Id, "HMM", null, IsOwner: true, IsOperator: false), TestContext.Current.CancellationToken);
        var rename = await handler.HandleAsync(
            new UpdateLineOperatorCommand(hmm.Id, "HMM Co., Ltd", "0300000001", IsOwner: true, IsOperator: true), TestContext.Current.CancellationToken);

        dropOwner.Error!.Code.Should().Be("Partner.OwnerRoleInUse");
        dropOperator.Error!.Code.Should().Be("Partner.OperatorRoleInUse");
        rename.IsSuccess.Should().BeTrue();
        hmm.Name.Should().Be("HMM Co., Ltd");
        hmm.IsOwner.Should().BeTrue();
    }

    [Fact]
    public void Container_Should_Only_Take_An_Owner_Partner()
    {
        var container = new Container("MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

        var result = container.SetOwner(new LineOperator("ONE", "Ocean Network Express", isOwner: false, isOperator: true));

        result.Error!.Code.Should().Be("Partner.NotOwner");
        container.OwnerId.Should().BeNull();
    }
}
