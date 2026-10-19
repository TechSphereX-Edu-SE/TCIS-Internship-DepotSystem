using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class ContainerNumberRulesTests
{
    [Theory]
    [InlineData("CSQU3054383")]   // ví dụ chuẩn của ISO 6346
    [InlineData("MSCU1234566")]
    [InlineData("hmmu2468109")]   // chữ thường vẫn hợp lệ sau khi chuẩn hóa
    [InlineData(" CMAU7654327 ")]
    public void IsValid_Should_Return_True_For_Valid_Numbers(string number)
    {
        ContainerNumberRules.IsValid(number).Should().BeTrue();
    }

    [Theory]
    [InlineData("CSQU3054384")]   // sai check digit
    [InlineData("MSKU1234567")]   // sai check digit
    [InlineData("CSQ3054383")]    // thiếu ký tự
    [InlineData("1SQU3054383")]   // ký tự đầu không phải chữ
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_Should_Return_False_For_Invalid_Numbers(string? number)
    {
        ContainerNumberRules.IsValid(number).Should().BeFalse();
    }

    [Fact]
    public void CalculateCheckDigit_Should_Match_Iso6346_Example()
    {
        ContainerNumberRules.CalculateCheckDigit("CSQU305438").Should().Be(3);
    }
}
