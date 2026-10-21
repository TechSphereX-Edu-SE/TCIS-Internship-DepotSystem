using FluentAssertions;
using TechSpherex.CleanArchitecture.Application.Features.Containers.Create;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class IsoCodeRulesTests
{
    [Theory]
    [InlineData("22G1", ContainerSize.Ft20, ContainerCategory.Dry)]
    [InlineData("45G1", ContainerSize.Ft40, ContainerCategory.Dry)]          // 40ft high cube
    [InlineData("2EG1", ContainerSize.Ft20, ContainerCategory.Dry)]          // pallet-wide 9ft6
    [InlineData("22GB", ContainerSize.Ft20, ContainerCategory.Dry)]          // alternate letter of G1
    [InlineData("45R1", ContainerSize.Ft40, ContainerCategory.Reefer)]
    [InlineData("22U1", ContainerSize.Ft20, ContainerCategory.OpenTop)]
    [InlineData("42P1", ContainerSize.Ft40, ContainerCategory.FlatRack)]
    [InlineData("22P0", ContainerSize.Ft20, ContainerCategory.FlatRack)]     // plain platform is still group P
    [InlineData("22T6", ContainerSize.Ft20, ContainerCategory.Bunker)]
    [InlineData("22V0", ContainerSize.Ft20, ContainerCategory.Ventilated)]
    [InlineData("22S1", ContainerSize.Ft20, ContainerCategory.Specialized)]  // automobile carrier
    [InlineData("22H5", ContainerSize.Ft20, ContainerCategory.Specialized)]  // insulated
    [InlineData("22B0", ContainerSize.Ft20, ContainerCategory.Specialized)]  // bulk
    [InlineData("45s1", ContainerSize.Ft40, ContainerCategory.Specialized)]  // lower case is normalized
    public void Should_Accept_Code_Matching_Size_And_Type(string isoCode, ContainerSize size, ContainerCategory category)
    {
        IsoCodeRules.IsValid(isoCode, size, category).Should().BeTrue();
    }

    [Theory]
    [InlineData("42G1", ContainerSize.Ft20, ContainerCategory.Dry)]          // 40ft length code on a 20ft
    [InlineData("22G1", ContainerSize.Ft40, ContainerCategory.Dry)]          // 20ft length code on a 40ft
    [InlineData("23G1", ContainerSize.Ft20, ContainerCategory.Dry)]          // 3 is not a height code
    [InlineData("22R1", ContainerSize.Ft20, ContainerCategory.Dry)]          // reefer group on a dry container
    [InlineData("22H1", ContainerSize.Ft20, ContainerCategory.Reefer)]       // H is not an integral reefer
    [InlineData("22G4", ContainerSize.Ft20, ContainerCategory.Dry)]          // G4 is not defined
    [InlineData("22V1", ContainerSize.Ft20, ContainerCategory.Ventilated)]   // V1 is not defined
    [InlineData("22B2", ContainerSize.Ft20, ContainerCategory.Specialized)]  // B2 is not defined
    [InlineData("22GZ", ContainerSize.Ft20, ContainerCategory.Dry)]          // Z is not an alternate letter
    [InlineData("22G", ContainerSize.Ft20, ContainerCategory.Dry)]           // too short
    [InlineData("2211", ContainerSize.Ft20, ContainerCategory.Dry)]          // no type group letter
    public void Should_Reject_Code_Not_Matching_Size_Or_Type(string isoCode, ContainerSize size, ContainerCategory category)
    {
        IsoCodeRules.IsValid(isoCode, size, category).Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Explain_What_Is_Allowed()
    {
        IsoCodeRules.Validate("22R1", ContainerSize.Ft20, ContainerCategory.Dry)
            .Should().Contain("must be G");
        IsoCodeRules.Validate("22G4", ContainerSize.Ft20, ContainerCategory.Dry)
            .Should().Contain("G0, G1, G2, G3");
    }

    [Fact]
    public void Create_Validator_Should_Reject_Iso_Code_Of_Other_Size()
    {
        var command = new CreateContainerCommand(
            "CSQU3054383", "42G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

        var result = new CreateContainerValidator().Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == "IsoCode");
    }
}
