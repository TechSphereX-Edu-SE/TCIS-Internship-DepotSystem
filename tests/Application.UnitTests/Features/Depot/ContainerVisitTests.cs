using FluentAssertions;
using TechSpherex.CleanArchitecture.Domain.Entities;
using TechSpherex.CleanArchitecture.Domain.Enums;
namespace TechSpherex.CleanArchitecture.Application.UnitTests.Features.Depot;

public sealed class ContainerVisitTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private readonly LineOperator _msc = new("MSC", "Mediterranean Shipping Company");
    private readonly Customer _customer = new("0301234567", "Customer A");
    private readonly Block _block = Block.CreatePhysical("A", 20, 6, 5);
    private readonly Container _container = new(
        "MSCU1234566", "22G1", ContainerSize.Ft20, ContainerCategory.Dry, 30480, 2200, null, "MSC");

    private ContainerVisit GateIn(DateTimeOffset at) =>
        ContainerVisit.GateIn(_container, _msc.Id, _block, 1, 1, 1, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", at).Value!;

    // Order with a single Dry line
    private DeliveryOrder Order(DateOnly expiry, int quantity = 1, Guid? lineOperatorId = null)
    {
        var order = new DeliveryOrder("DO001", lineOperatorId ?? _msc.Id, _customer.Id, expiry, "MSC ANNA 042W");
        order.AddLine(ContainerCategory.Dry, quantity);
        return order;
    }

    [Fact]
    public void GateIn_Should_Fail_When_Bay_Does_Not_Match_Size()
    {
        var result = ContainerVisit.GateIn(_container, _msc.Id, _block, 2, 1, 1, ContainerGrade.A,
            ContainerCondition.Normal, null, "51C-12345", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GateOut_Should_Succeed_With_Valid_Order()
    {
        var visit = GateIn(new DateTimeOffset(2026, 10, 1, 8, 0, 0, Vn));
        var order = Order(new DateOnly(2026, 10, 31));

        var result = visit.GateOut(order, _container.Category, "51C-99999",
            new DateTimeOffset(2026, 10, 15, 9, 0, 0, Vn));

        result.IsSuccess.Should().BeTrue();
        visit.IsInYard.Should().BeFalse();
        order.Lines.Single().RemainingQuantity.Should().Be(0);
        visit.DwellDays(DateTimeOffset.UtcNow).Should().Be(14);
    }

    [Fact]
    public void GateOut_Should_Fail_When_Order_Expired()
    {
        var visit = GateIn(new DateTimeOffset(2026, 10, 1, 8, 0, 0, Vn));
        var order = Order(new DateOnly(2026, 10, 10));

        var result = visit.GateOut(order, _container.Category, "51C-99999",
            new DateTimeOffset(2026, 10, 11, 9, 0, 0, Vn));

        result.Error!.Code.Should().Be("DeliveryOrder.Expired");
        visit.IsInYard.Should().BeTrue();
    }

    [Fact]
    public void GateOut_Should_Fail_When_Quantity_Used_Up()
    {
        var order = Order(new DateOnly(2026, 10, 31), quantity: 1);
        var at = new DateTimeOffset(2026, 10, 15, 9, 0, 0, Vn);
        GateIn(at.AddDays(-5)).GateOut(order, ContainerCategory.Dry, "51C-1", at);

        var second = GateIn(at.AddDays(-3));
        var result = second.GateOut(order, ContainerCategory.Dry, "51C-2", at);

        result.Error!.Code.Should().Be("DeliveryOrder.QuantityExceeded");
    }

    [Fact]
    public void GateOut_Should_Fail_When_Order_Belongs_To_Other_Line()
    {
        var visit = GateIn(new DateTimeOffset(2026, 10, 1, 8, 0, 0, Vn));
        var order = Order(new DateOnly(2026, 10, 31), lineOperatorId: Guid.NewGuid());

        var result = visit.GateOut(order, _container.Category, "51C-99999",
            new DateTimeOffset(2026, 10, 15, 9, 0, 0, Vn));

        result.Error!.Code.Should().Be("ContainerVisit.WrongLineOperator");
    }

    [Fact]
    public void GateOut_Should_Fail_When_Order_Has_No_Line_For_Category()
    {
        var visit = GateIn(new DateTimeOffset(2026, 10, 1, 8, 0, 0, Vn));
        var order = new DeliveryOrder("DO002", _msc.Id, _customer.Id, new DateOnly(2026, 10, 31), "MSC ANNA 042W");
        order.AddLine(ContainerCategory.Reefer, 2);

        var result = visit.GateOut(order, _container.Category, "51C-99999",
            new DateTimeOffset(2026, 10, 15, 9, 0, 0, Vn));

        result.Error!.Code.Should().Be("DeliveryOrder.TypeNotAllowed");
        visit.IsInYard.Should().BeTrue();
    }

    [Fact]
    public void AddLine_Should_Reject_Duplicate_Category_And_Invalid_Quantity()
    {
        var order = new DeliveryOrder("DO003", _msc.Id, _customer.Id, new DateOnly(2026, 10, 31), "MSC ANNA 042W");
        order.AddLine(ContainerCategory.Dry, 3).IsSuccess.Should().BeTrue();

        order.AddLine(ContainerCategory.Dry, 1).Error!.Code.Should().Be("DeliveryOrder.DuplicateLine");
        order.AddLine(ContainerCategory.Reefer, 0).Error!.Code.Should().Be("DeliveryOrder.InvalidQuantity");
        order.Lines.Should().ContainSingle();
    }
}
