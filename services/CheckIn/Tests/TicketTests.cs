namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using VenuePulse.CheckIn.Domain;

public class TicketTests
{
    private static Ticket NewPendingTicket() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ana");

    [Fact]
    public void Una_entrada_nace_pendiente()
    {
        var ticket = NewPendingTicket();

        ticket.Status.Should().Be(TicketStatus.Pending);
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
    public void Confirmar_una_entrada_esperando_pago_funciona()
    {
        var ticket = NewPendingTicket();
        ticket.MarkAwaitingPayment();

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
}
