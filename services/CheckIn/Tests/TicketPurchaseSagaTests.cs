namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Tests de aplicación con el harness in-memory de MassTransit (sin broker ni Mongo reales) —
/// prueban la lógica de la state machine en aislamiento, camino feliz y compensación.
/// Los tests con infraestructura real (Postgres+RabbitMQ+Mongo) viven en IntegrationTests.
/// </summary>
public class TicketPurchaseSagaTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            // Delay a cero: el arnés in-memory prueba la lógica de la state machine, no el paso
            // del tiempo real — mismo criterio que Saga:PaymentProcessingDelaySeconds=0 en
            // IntegrationTests (ver .ia/KNOWN-ISSUES.md sobre no añadir flakiness al arnés).
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

    // Consumed.Any<T>() en este arnés in-memory puede resolver antes de que el Publish anidado
    // dentro de ese mismo consumo (p.ej. la Saga publicando TicketPurchaseRejected como parte de
    // procesar SlotReservationFailed) quede registrado en Published — reintenta en vez de asumir
    // que un único chequeo inmediato ya refleja el efecto completo del mensaje consumido.
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

        // El aforo ya está reservado, pero la Saga espera aquí a que el cliente elija método de
        // pago (Tarjeta/Bizum) en vez de confirmar sola. TicketAwaitingPayment lo publica la
        // propia Saga tras su transición (no un consumer en paralelo que la adivine) — ver ADR
        // 0018, evita la ventana real donde el Ticket en Postgres podía ir por delante de la Saga.
        var sagaHarness = provider.GetRequiredService<ISagaStateMachineTestHarness<TicketPurchaseSaga, TicketPurchaseSagaState>>();
        sagaHarness.Sagas.Contains(ticketId)?.CurrentState.Should().Be("AwaitingPayment");
        (await harness.Published.Any<TicketAwaitingPayment>()).Should().BeTrue();

        await harness.Bus.Publish(new TicketPaymentSubmitted(ticketId, SimulateFailure: false));
        // Publicado ANTES de resolver Confirmado/Rechazado (ver ADR 0021) — ilumina el timeline
        // por SignalR mientras la Saga "procesa" el pago (delay a cero en este arnés).
        (await harness.Published.Any<TicketProcessingPayment>()).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotConfirmed(ticketId, zoneId));
        (await harness.Published.Any<TicketPurchaseConfirmed>()).Should().BeTrue();

        // SetCompletedWhenFinalized() borra la instancia del repositorio al finalizar —
        // por eso la prueba de "llegó a Completed" es la publicación de TicketPurchaseConfirmed
        // (arriba), no una consulta de existencia contra un estado final ya eliminado.
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

        // El aforo todavía no llegó a reservarse en este punto — el evento se cancela antes de que
        // AccessControl responda (ver ADR 0021, mismo razonamiento que TimedOut en Reserving).
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

        // El cliente todavía no eligió método de pago — a diferencia de la cancelación en
        // Reserving, aquí el aforo SÍ está reservado de verdad y hace falta liberarlo.
        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        await harness.Consumed.Any<ZoneSlotReleased>();
        (await harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "EventCancelled")).Should().BeTrue();
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

        // Mismo mensaje que la cascada de EventCancelled, pero con un motivo propio (ver
        // CancelOrderCommand) — el Reason debe viajar hasta el rechazo final en vez de quedar
        // hardcodeado como "EventCancelled".
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

        // El watchdog (fuera de la Saga, ver ADR 0010) publica ReservationTimedOut tras el
        // deadline, pero la Saga ya no rechaza directamente (ver ADR 0027) — espera a saber de
        // verdad si AccessControl llegó a reservar o no antes de decidir si hace falta compensar.
        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();
        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeFalse();

        // AccessControl confirma (aunque tarde) que el aforo nunca llegó a reservarse — nada que
        // compensar.
        await harness.Bus.Publish(new ZoneSlotReservationFailed(ticketId, zoneId, "ZoneFull"));

        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseRejected>(m => m.Context.Message.Reason == "ReservationTimedOut"))).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeFalse();
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeFalse();
    }

    [Fact]
    public async Task Reserva_confirmada_tras_el_timeout_se_libera_en_vez_de_quedar_fantasma()
    {
        // Reproduce el bug real (aforo "fantasma": reservado en AccessControl, nunca liberado ni
        // confirmado) descrito en ADR 0027 — el watchdog se rinde justo antes de que la respuesta
        // real de AccessControl (que SÍ reservó el aforo) llegue de vuelta a la Saga.
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();

        await harness.Bus.Publish(new ReservationTimedOut(ticketId, zoneId));
        await harness.Consumed.Any<ReservationTimedOut>();
        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeFalse();

        // La respuesta real de AccessControl llega después de todo, confirmando que el aforo SÍ
        // se reservó — antes de este fix, este mensaje se perdía (la Saga ya había finalizado) y
        // el aforo se quedaba reservado para siempre.
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

        // El cliente nunca elige método de pago — a diferencia del timeout en Reserving, aquí el
        // aforo SÍ está reservado de verdad y hace falta liberarlo.
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
