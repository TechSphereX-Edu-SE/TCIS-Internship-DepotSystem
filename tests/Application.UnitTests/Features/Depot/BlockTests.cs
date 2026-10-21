using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class BlockTests
{
    private readonly Block _block = Block.CreatePhysical("A", maxBays: 10, maxRows: 4, maxTiers: 3);

    [Theory]
    [InlineData(1, ContainerSize.Ft20)]
    [InlineData(2, ContainerSize.Ft40)]
    public void ValidateSlot_Should_Accept_Correct_Bay_Parity(int bay, ContainerSize size)
    {
        _block.ValidateSlot(bay, 1, 1, size).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, ContainerSize.Ft20)]
    [InlineData(3, ContainerSize.Ft40)]
    public void ValidateSlot_Should_Reject_Wrong_Bay_Parity(int bay, ContainerSize size)
    {
        var result = _block.ValidateSlot(bay, 1, 1, size);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Block.BayParity");
    }

    [Fact]
    public void ValidateSlot_Should_Reject_Position_Outside_Block()
    {
        _block.ValidateSlot(1, 5, 1, ContainerSize.Ft20).Error!.Code.Should().Be("Block.SlotOutOfRange");
    }

    [Fact]
    public void Virtual_Block_Should_Not_Accept_Bay_Row_Tier()
    {
        var virtualBlock = Block.CreateVirtual("WAIT");

        virtualBlock.ValidateSlot(null, null, null, ContainerSize.Ft40).IsSuccess.Should().BeTrue();
        virtualBlock.ValidateSlot(1, 1, 1, ContainerSize.Ft20).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidateSlot_Should_Reject_40ft_In_Last_Even_Bay()
    {
        // Block has 10 bays: a 40ft container in bay 10 would also cover bay 11
        _block.ValidateSlot(10, 1, 1, ContainerSize.Ft40).Error!.Code.Should().Be("Block.BayOutOfRange");
        _block.ValidateSlot(8, 1, 1, ContainerSize.Ft40).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Resize_Should_Require_At_Least_Two_Tiers()
    {
        var block = Block.CreatePhysical("Z", 10, 4, 3);

        block.Resize(10, 4, 1).Error!.Code.Should().Be("Block.InvalidSize");
        block.Resize(10, 4, 2).IsSuccess.Should().BeTrue();
    }
}
