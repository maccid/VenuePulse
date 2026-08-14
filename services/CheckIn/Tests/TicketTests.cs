namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using VenuePulse.CheckIn.Domain;

public class TicketTests
{
    private static Ticket NewPendingTicket(decimal price = 40.00m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ana", price);

    [Fact]
    public void Una_entrada_nace_pendiente_con_su_precio()
    {
        var ticket = NewPendingTicket(95.00m);

        ticket.Status.Should().Be(TicketStatus.Pending);
        ticket.Price.Should().Be(95.00m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void No_se_puede_crear_una_entrada_con_precio_no_positivo(decimal price)
    {
        var act = () => NewPendingTicket(price);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void No_se_puede_confirmar_una_entrada_ya_rechazada()
    {
        var ticket = NewPendingTicket();
        ticket.Reject("ZoneFull");

        var act = ticket.Confirm;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_hacer_check_in_de_una_entrada_pendiente()
    {
        var ticket = NewPendingTicket();

        var act = ticket.MarkCheckedIn;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_hacer_check_in_dos_veces()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();
        ticket.MarkProcessingPayment();
        ticket.Confirm();
        ticket.MarkCheckedIn();

        var act = ticket.MarkCheckedIn;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_confirmar_una_entrada_pendiente_sin_pasar_por_esperando_pago()
    {
        var ticket = NewPendingTicket();

        var act = ticket.Confirm;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_confirmar_una_entrada_esperando_pago_sin_pasar_por_procesando_pago()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();

        var act = ticket.Confirm;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirmar_una_entrada_procesando_pago_funciona()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();
        ticket.MarkProcessingPayment();

        ticket.Confirm();

        ticket.Status.Should().Be(TicketStatus.Confirmed);
        ticket.ConfirmedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Se_puede_rechazar_una_entrada_esperando_pago()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();

        ticket.Reject("PaymentFailed");

        ticket.Status.Should().Be(TicketStatus.Rejected);
    }

    [Fact]
    public void Se_puede_rechazar_una_entrada_procesando_pago()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();
        ticket.MarkProcessingPayment();

        ticket.Reject("PaymentFailed");

        ticket.Status.Should().Be(TicketStatus.Rejected);
    }

    [Fact]
    public void Solo_una_entrada_confirmada_se_puede_reembolsar()
    {
        var ticket = NewPendingTicket();

        var act = ticket.Refund;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reembolsar_una_entrada_confirmada_la_deja_en_estado_terminal_Refunded()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();
        ticket.MarkProcessingPayment();
        ticket.Confirm();

        ticket.Refund();

        ticket.Status.Should().Be(TicketStatus.Refunded);
    }

    [Fact]
    public void Una_entrada_nace_sin_pedido_asignado()
    {
        var ticket = NewPendingTicket();

        ticket.GroupId.Should().BeNull();
    }

    [Fact]
    public void Se_puede_vincular_una_entrada_a_un_pedido()
    {
        var ticket = NewPendingTicket();
        var orderId = Guid.NewGuid();

        ticket.AssignToGroup(orderId);

        ticket.GroupId.Should().Be(orderId);
    }

    [Fact]
    public void No_se_puede_vincular_una_entrada_a_un_pedido_dos_veces()
    {
        var ticket = NewPendingTicket();
        ticket.AssignToGroup(Guid.NewGuid());

        var act = () => ticket.AssignToGroup(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
