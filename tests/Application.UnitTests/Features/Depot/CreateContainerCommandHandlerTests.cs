using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Create;
using TechSpherex.CleanArchitecture.Domain.Common;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Infrastructure.Persistence;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class CreateContainerCommandHandlerTests
{
    // Validator tests only need a non-empty owner id; handler tests pass a seeded owner
    private static CreateContainerCommand Command(string number = "CSQU3054383", Guid? ownerId = null) =>
        new(number, "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, new DateOnly(2020, 1, 1),
            ownerId ?? Guid.NewGuid());

    private static async Task<LineOperator> SeedPartnerAsync(AppDbContext dbContext, bool isOwner = true)
    {
        var partner = new LineOperator("MSC", "Mediterranean Shipping Company", isOwner: isOwner, isOperator: true);
        dbContext.LineOperators.Add(partner);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return partner;
    }

    [Fact]
    public async Task HandleAsync_Should_Create_Container()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var owner = await SeedPartnerAsync(dbContext);
        var handler = new CreateContainerCommandHandler(dbContext);

        var result = await handler.HandleAsync(Command(ownerId: owner.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var container = dbContext.Containers.Single(c => c.ContainerNumber == "CSQU3054383");
        container.OwnerId.Should().Be(owner.Id);
        container.OwnerName.Should().Be("MSC");
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Conflict_When_Number_Exists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var owner = await SeedPartnerAsync(dbContext);
        var handler = new CreateContainerCommandHandler(dbContext);
        await handler.HandleAsync(Command(ownerId: owner.Id), TestContext.Current.CancellationToken);

        var result = await handler.HandleAsync(Command(" csqu3054383 ", owner.Id), TestContext.Current.CancellationToken);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task HandleAsync_Should_Reject_An_Owner_That_Is_Not_In_The_Catalog_Or_Has_No_Owner_Role()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var operatorOnly = await SeedPartnerAsync(dbContext, isOwner: false);
        var handler = new CreateContainerCommandHandler(dbContext);

        var unknown = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);
        var notOwner = await handler.HandleAsync(Command(ownerId: operatorOnly.Id), TestContext.Current.CancellationToken);

        unknown.Error!.Type.Should().Be(ErrorType.NotFound);
        notOwner.Error!.Code.Should().Be("Partner.NotOwner");
        dbContext.Containers.Should().BeEmpty();
    }

    [Fact]
    public void Validator_Should_Require_The_Owner()
    {
        var result = new CreateContainerValidator().Validate(Command() with { OwnerId = Guid.Empty });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "OwnerId");
    }

    [Fact]
    public void Validator_Should_Reject_Wrong_Check_Digit()
    {
        var result = new CreateContainerValidator().Validate(Command("CSQU3054384"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Reject_Type_Not_Matching_Iso_Code()
    {
        // The type is checked against the ISO code: 22G1 is group G (Dry), not Reefer
        var result = new CreateContainerValidator().Validate(Command() with { Category = ContainerCategory.Reefer });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "IsoCode");
    }

    [Fact]
    public void Validator_Should_Accept_Reefer_With_U_And_Reefer_Iso_Code()
    {
        // ISO 6346: every freight container uses U; the reefer type comes from the ISO code
        var command = Command("HMMU2468109") with { IsoCode = "22R1", Category = ContainerCategory.Reefer };

        new CreateContainerValidator().Validate(command).IsValid.Should().BeTrue();
    }
}
