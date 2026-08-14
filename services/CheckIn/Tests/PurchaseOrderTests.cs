namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using VenuePulse.CheckIn.Domain;

public class PurchaseOrderTests
{
    private static PurchaseOrder NewReservingOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Ana");

    [Fact]
    public void Un_pedido_nace_reservando_lineas()
    {
        var order = NewReservingOrder();

        order.Status.Should().Be(PurchaseOrderStatus.Reserving);
    }

    [Fact]
    public void No_se_puede_crear_un_pedido_sin_titular()
    {
        var act = () => new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reservando_lineas_puede_pasar_a_esperando_pago()
    {
        var order = NewReservingOrder();

        order.MarkAwaitingPayment();

        order.Status.Should().Be(PurchaseOrderStatus.AwaitingPayment);
    }

    [Fact]
    public void No_se_puede_esperar_pago_dos_veces()
    {
        var order = NewReservingOrder();
        order.MarkAwaitingPayment();

        var act = order.MarkAwaitingPayment;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_confirmar_lineas_sin_pasar_por_esperando_pago()
    {
        var order = NewReservingOrder();

        var act = order.MarkConfirming;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Esperando_pago_puede_pasar_a_confirmando_lineas()
    {
        var order = NewReservingOrder();
        order.MarkAwaitingPayment();

        order.MarkConfirming();

        order.Status.Should().Be(PurchaseOrderStatus.Confirming);
    }

    [Fact]
    public void No_se_puede_completar_un_pedido_sin_pasar_por_confirmando_lineas()
    {
        var order = NewReservingOrder();
        order.MarkAwaitingPayment();

        var act = order.Complete;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirmando_lineas_puede_completarse()
    {
        var order = NewReservingOrder();
        order.MarkAwaitingPayment();
        order.MarkConfirming();

        order.Complete();

        order.Status.Should().Be(PurchaseOrderStatus.Completed);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Reserving)]
    [InlineData(PurchaseOrderStatus.AwaitingPayment)]
    [InlineData(PurchaseOrderStatus.Confirming)]
    public void Se_puede_rechazar_un_pedido_no_finalizado(PurchaseOrderStatus status)
    {
        var order = NewReservingOrder();
        if (status is PurchaseOrderStatus.AwaitingPayment or PurchaseOrderStatus.Confirming)
            order.MarkAwaitingPayment();
        if (status is PurchaseOrderStatus.Confirming)
            order.MarkConfirming();

        order.Reject("PriceChanged");

        order.Status.Should().Be(PurchaseOrderStatus.Rejected);
        order.RejectionReason.Should().Be("PriceChanged");
    }

    [Fact]
    public void No_se_puede_rechazar_un_pedido_ya_completado()
    {
        var order = NewReservingOrder();
        order.MarkAwaitingPayment();
        order.MarkConfirming();
        order.Complete();

        var act = () => order.Reject("PaymentFailed");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_rechazar_un_pedido_ya_rechazado()
    {
        var order = NewReservingOrder();
        order.Reject("PriceChanged");

        var act = () => order.Reject("InsufficientAvailability");

        act.Should().Throw<InvalidOperationException>();
    }
}
