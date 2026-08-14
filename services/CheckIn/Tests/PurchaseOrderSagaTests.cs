namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Tests de aplicación con el harness in-memory de MassTransit (sin broker ni Mongo reales, mismo
/// patrón que <see cref="TicketPurchaseSagaTests"/>) — prueban la <see cref="PurchaseOrderSaga"/>
/// orquestadora EN AISLAMIENTO, publicando a mano los eventos que en producción publicaría cada
/// <c>TicketPurchaseSaga</c> hermana (ver ADR 0028): esta Saga nunca sabe de la existencia de esas
/// Sagas, solo observa <see cref="TicketAwaitingPayment"/>/<see cref="TicketPurchaseConfirmed"/>/
/// <see cref="TicketPurchaseRejected"/> correlacionados por <c>GroupId</c>.
/// </summary>
public class PurchaseOrderSagaTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<PurchaseOrderSaga, PurchaseOrderSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness);
    }

    // Mismo motivo que en TicketPurchaseSagaTests: un Publish anidado dentro de la resolución de
    // otro mensaje (p. ej. PurchaseOrderRejected publicado al procesar el último TicketPurchaseRejected
    // pendiente) puede no estar todavía en Published cuando Consumed.Any ya se resolvió.
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
    public async Task Camino_feliz_con_dos_lineas_completa_el_pedido()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticket1 = Guid.NewGuid();
        var zone1 = Guid.NewGuid();
        var ticket2 = Guid.NewGuid();
        var zone2 = Guid.NewGuid();

        await harness.Bus.Publish(new PurchaseOrderRequested(
            orderId, eventId, [new PurchaseOrderTicketRef(ticket1, zone1), new PurchaseOrderTicketRef(ticket2, zone2)]));
        await harness.Consumed.Any<PurchaseOrderRequested>();

        // Ambas líneas reservaron aforo con éxito (publicado por cada TicketPurchaseSaga hermana,
        // simulado a mano aquí) — solo cuando las DOS avisan, el pedido pasa a esperar pago.
        await harness.Bus.Publish(new TicketAwaitingPayment(ticket1, orderId));
        (await harness.Published.Any<PurchaseOrderAwaitingPayment>()).Should().BeFalse();

        await harness.Bus.Publish(new TicketAwaitingPayment(ticket2, orderId));
        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderAwaitingPayment>())).Should().BeTrue();

        await harness.Bus.Publish(new PurchaseOrderPaymentSubmitted(orderId, SimulateFailure: false));
        (await EventuallyAsync(() => harness.Published.Any<TicketPaymentSubmitted>(m => m.Context.Message.CorrelationId == ticket1))).Should().BeTrue();
        (await harness.Published.Any<TicketPaymentSubmitted>(m => m.Context.Message.CorrelationId == ticket2)).Should().BeTrue();

        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket1, orderId));
        (await harness.Published.Any<PurchaseOrderCompleted>()).Should().BeFalse();

        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket2, orderId));
        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderCompleted>())).Should().BeTrue();
    }

    [Fact]
    public async Task Una_linea_sin_aforo_cancela_la_hermana_y_rechaza_el_pedido_completo()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticket1 = Guid.NewGuid();
        var zone1 = Guid.NewGuid();
        var ticket2 = Guid.NewGuid();
        var zone2 = Guid.NewGuid();

        await harness.Bus.Publish(new PurchaseOrderRequested(
            orderId, eventId, [new PurchaseOrderTicketRef(ticket1, zone1), new PurchaseOrderTicketRef(ticket2, zone2)]));
        await harness.Consumed.Any<PurchaseOrderRequested>();

        // La línea 1 no consigue reservar aforo (AccessControl la rechazó) mientras la línea 2
        // seguía reservando — hay que cancelarla, no dejarla huérfana.
        await harness.Bus.Publish(new TicketPurchaseRejected(ticket1, "ZoneFull", orderId));
        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseCancelled>(m => m.Context.Message.CorrelationId == ticket2))).Should().BeTrue();

        // La cancelación llega a la TicketPurchaseSaga de la línea 2 (simulado a mano aquí, ver
        // TicketPurchaseSagaTests.Cancelacion_de_evento_en_Reserving_rechaza_sin_liberar_aforo) —
        // como todavía no había reservado aforo, se rechaza directamente.
        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "EventCancelled", orderId));

        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "InsufficientAvailability"))).Should().BeTrue();
    }

    [Fact]
    public async Task Una_linea_rechazada_al_pagar_compensa_a_la_hermana_ya_confirmada()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticket1 = Guid.NewGuid();
        var zone1 = Guid.NewGuid();
        var ticket2 = Guid.NewGuid();
        var zone2 = Guid.NewGuid();

        await harness.Bus.Publish(new PurchaseOrderRequested(
            orderId, eventId, [new PurchaseOrderTicketRef(ticket1, zone1), new PurchaseOrderTicketRef(ticket2, zone2)]));
        await harness.Consumed.Any<PurchaseOrderRequested>();

        await harness.Bus.Publish(new TicketAwaitingPayment(ticket1, orderId));
        await harness.Bus.Publish(new TicketAwaitingPayment(ticket2, orderId));
        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderAwaitingPayment>())).Should().BeTrue();

        await harness.Bus.Publish(new PurchaseOrderPaymentSubmitted(orderId, SimulateFailure: false));
        await harness.Consumed.Any<PurchaseOrderPaymentSubmitted>();

        // La línea 1 confirma su pago; la línea 2 lo rechaza (p. ej. Bizum) — el pedido completo se
        // rechaza y hay que compensar la línea 1, que ya estaba vendida de verdad.
        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket1, orderId));
        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "PaymentFailed", orderId));

        (await EventuallyAsync(() => harness.Published.Any<ReleaseConfirmedZoneSlot>(m => m.Context.Message.CorrelationId == ticket1 && m.Context.Message.ZoneId == zone1))).Should().BeTrue();
        (await harness.Published.Any<TicketRefunded>(m => m.Context.Message.CorrelationId == ticket1)).Should().BeTrue();
        (await harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "PaymentFailed")).Should().BeTrue();

        // La línea 2 (la que falló) nunca se compensa con estos dos mensajes — ya está rechazada.
        (await harness.Published.Any<ReleaseConfirmedZoneSlot>(m => m.Context.Message.CorrelationId == ticket2)).Should().BeFalse();
    }
}
