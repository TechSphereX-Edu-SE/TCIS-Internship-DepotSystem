using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Enums;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class ContainerNumberRulesTests
{
    [Theory]
    [InlineData("CSQU3054383")]   // ISO 6346 reference example
    [InlineData("MSCU1234566")]
    [InlineData("CMAU1234564")]   // Appendix I example with the correctly computed check digit
    [InlineData("CMAU100000X")]   // remainder 10 -> X (Appendix I)
    [InlineData("CMAU9001110")]   // remainder 0 -> 0
    [InlineData("HMMR2468107")]   // type code R (Reefer)
    [InlineData("MSCP123456X")]   // type code P (Specialized), remainder 10
    [InlineData("hmmu2468109")]   // lowercase is normalized
    [InlineData("cmau100000x")]   // lowercase x is normalized to X
    [InlineData(" CMAU7654327 ")] // surrounding spaces are trimmed
    public void IsValid_Should_Return_True_For_Valid_Numbers(string number)
    {
        ContainerNumberRules.IsValid(number).Should().BeTrue();
    }

    [Theory]
    [InlineData("CSQU3054384")]   // wrong check digit
    [InlineData("CMAU1234567")]   // Appendix I example with its assumed digit 7
    [InlineData("CMAU1000000")]   // remainder 10 written as 0 instead of X
    [InlineData("CSQU305438X")]   // X used although remainder is not 10
    [InlineData("CSQU30543X3")]   // X is only allowed in the last position
    [InlineData("CSQ3054383")]    // too short
    [InlineData("1SQU3054383")]   // first character is not a letter
    [InlineData("CMAU0000005")]   // serial 000000 is not issued (serial starts at 000001)
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_Should_Return_False_For_Invalid_Numbers(string? number)
    {
        ContainerNumberRules.IsValid(number).Should().BeFalse();
    }

    [Theory]
    [InlineData("CSQU305438", '3')]
    [InlineData("CMAU123456", '4')]
    [InlineData("CMAU100000", 'X')]
    [InlineData("CMAU900111", '0')]
    public void CalculateCheckDigit_Should_Follow_Appendix_I(string firstTen, char expected)
    {
        ContainerNumberRules.CalculateCheckDigit(firstTen).Should().Be(expected);
    }

    [Theory]
    [InlineData("MSCU1234566", ContainerCategory.Dry, true)]
    [InlineData("HMMR2468107", ContainerCategory.Reefer, true)]
    [InlineData("CMAS123456X", ContainerCategory.OpenTop, true)]
    [InlineData("CMAF1234568", ContainerCategory.FlatRack, true)]
    [InlineData("MSCB1234560", ContainerCategory.Bunker, true)]
    [InlineData("MSCV1234560", ContainerCategory.Ventilated, true)]
    [InlineData("MSCP123456X", ContainerCategory.Specialized, true)]
    [InlineData("MSCU1234566", ContainerCategory.Reefer, false)]
    [InlineData("HMMR2468107", ContainerCategory.Dry, false)]
    public void MatchesCategory_Should_Compare_Type_Code_With_Category(string number, ContainerCategory category, bool expected)
    {
        ContainerNumberRules.MatchesCategory(number, category).Should().Be(expected);
    }
}
