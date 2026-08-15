namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.Core.Messaging.Contracts;

public class TicketPurchaseSagaTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddSingleton(new PaymentProcessingOptions(TimeSpan.Zero))
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<TicketPurchaseSaga, TicketPurchaseSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness);
    }

    private static async Task<bool> EventuallyAsync(Func<Task<bool>> check, int timeoutMs = 3000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            if (await check()) return true;
            await Task.Delay(20);
        } while (Environment.TickCount64 < deadline);

        return false;
    }

    [Fact]
    public async Task Camino_feliz_confirma_la_compra()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        (await harness.Published.Any<ReserveZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        var sagaHarness = provider.GetRequiredService<ISagaStateMachineTestHarness<TicketPurchaseSaga, TicketPurchaseSagaState>>();
        sagaHarness.Sagas.Contains(ticketId)?.CurrentState.Should().Be("AwaitingPayment");
        (await harness.Published.Any<TicketAwaitingPayment>()).Should().BeTrue();

        await harness.Bus.Publish(new TicketPaymentSubmitted(ticketId, SimulateFailure: false));
        (await harness.Published.Any<TicketProcessingPayment>()).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotConfirmed(ticketId, zoneId));
        (await harness.Published.Any<TicketPurchaseConfirmed>()).Should().BeTrue();
    }

    [Fact]
    public async Task Cancelacion_de_evento_en_Reserving_rechaza_sin_liberar_aforo()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId));
        await harness.Consumed.Any<TicketPurchaseCancelled>();

        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "EventCancelled")).Should().BeTrue();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelacion_de_evento_en_AwaitingPayment_libera_el_aforo_ya_reservado()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "EventCancelled")).Should().BeTrue();
    }

    [Fact]
    public async Task Timeout_tardio_en_Confirming_se_ignora_sin_bloquear_la_resolucion()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId, "CancelledByCustomer"));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "CancelledByCustomer")).Should().BeTrue();
    }

    [Fact]
    public async Task Cancelacion_por_el_comprador_en_Reserving_rechaza_con_el_motivo_propio()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId, "CancelledByCustomer"));
        await harness.Consumed.Any<TicketPurchaseCancelled>();

        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "CancelledByCustomer")).Should().BeTrue();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelacion_por_el_comprador_en_AwaitingPayment_libera_el_aforo_ya_reservado()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId, "CancelledByCustomer"));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "CancelledByCustomer")).Should().BeTrue();
    }

    [Fact]
    public async Task Timeout_tardio_en_Confirming_se_ignora_sin_romper_la_saga()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId, "EventCancelled"));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "EventCancelled")).Should().BeTrue();
    }

    [Fact]
    public async Task Camino_de_compensacion_por_bizum_libera_el_aforo_reservado()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new TicketPaymentSubmitted(ticketId, SimulateFailure: true));
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "PaymentFailed")).Should().BeTrue();
    }

    [Fact]
    public async Task Timeout_de_reserva_rechaza_la_compra_sin_confirmar_ni_liberar()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        (await harness.Published.Any<ReserveZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();
        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeFalse();

        await harness.Bus.Publish(new ZoneSlotReservationFailed(ticketId, zoneId, "ZoneFull"));

        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "ReservationTimedOut"))).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeFalse();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeFalse();
    }

    [Fact]
    public async Task Reserva_confirmada_tras_el_timeout_se_libera_en_vez_de_quedar_fantasma()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();
        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeFalse();

        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));

        (await EventuallyAsync(() => harness.Published.Any<ReleaseZoneSlot>())).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeFalse();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));

        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "ReservationTimedOut"))).Should().BeTrue();
    }

    [Fact]
    public async Task Timeout_esperando_pago_libera_el_aforo_ya_reservado()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReserved>();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "PaymentTimedOut")).Should().BeTrue();
    }

    [Fact]
    public async Task Zona_llena_rechaza_directamente_sin_llegar_a_esperar_pago()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        await harness.Bus.Publish(new ZoneSlotReservationFailed(ticketId, zoneId, "ZoneFull"));
        await harness.Consumed.Any<ZoneSlotReservationFailed>();

        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeFalse();
    }
}
