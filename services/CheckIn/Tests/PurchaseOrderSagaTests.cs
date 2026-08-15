namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.Core.Messaging.Contracts;

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

        await harness.Bus.Publish(new TicketPurchaseRejected(ticket1, "ZoneFull", orderId));
        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseCancelled>(m => m.Context.Message.CorrelationId == ticket2))).Should().BeTrue();

        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "EventCancelled", orderId));

        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "InsufficientAvailability"))).Should().BeTrue();
    }

    [Fact]
    public async Task Pedido_de_una_sola_linea_sin_aforo_rechaza_sin_quedarse_esperando_hermanas_inexistentes()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticket1 = Guid.NewGuid();
        var zone1 = Guid.NewGuid();

        await harness.Bus.Publish(new PurchaseOrderRequested(
            orderId, eventId, [new PurchaseOrderTicketRef(ticket1, zone1)]));
        await harness.Consumed.Any<PurchaseOrderRequested>();

        await harness.Bus.Publish(new TicketPurchaseRejected(ticket1, "ZoneFull", orderId));

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

        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket1, orderId));
        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "PaymentFailed", orderId));

        (await EventuallyAsync(() => harness.Published.Any<ReleaseConfirmedZoneSlot>(m => m.Context.Message.CorrelationId == ticket1 && m.Context.Message.ZoneId == zone1))).Should().BeTrue();
        (await harness.Published.Any<TicketRefunded>(m => m.Context.Message.CorrelationId == ticket1)).Should().BeTrue();
        (await harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "PaymentFailed")).Should().BeTrue();

        (await harness.Published.Any<ReleaseConfirmedZoneSlot>(m => m.Context.Message.CorrelationId == ticket2)).Should().BeFalse();
    }

    [Fact]
    public async Task Timeout_de_pago_de_una_linea_en_AwaitingPayment_cancela_la_hermana_y_rechaza_el_pedido()
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

        await harness.Bus.Publish(new TicketPurchaseRejected(ticket1, "PaymentTimedOut", orderId));

        (await EventuallyAsync(() => harness.Published.Any<TicketPurchaseCancelled>(m => m.Context.Message.CorrelationId == ticket2))).Should().BeTrue();

        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "EventCancelled", orderId));

        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "PaymentTimedOut"))).Should().BeTrue();
    }

    [Fact]
    public async Task Confirmacion_de_linea_antes_de_persistir_ConfirmingLines_completa_el_pedido_de_una_sola_linea()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticket1 = Guid.NewGuid();
        var zone1 = Guid.NewGuid();

        await harness.Bus.Publish(new PurchaseOrderRequested(
            orderId, eventId, [new PurchaseOrderTicketRef(ticket1, zone1)]));
        await harness.Consumed.Any<PurchaseOrderRequested>();

        await harness.Bus.Publish(new TicketAwaitingPayment(ticket1, orderId));
        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderAwaitingPayment>())).Should().BeTrue();

        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket1, orderId));

        (await EventuallyAsync(() => harness.Published.Any<PurchaseOrderCompleted>())).Should().BeTrue();
    }

    [Fact]
    public async Task Rechazo_de_linea_ya_pagada_antes_de_persistir_ConfirmingLines_rechaza_el_pedido()
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

        await harness.Bus.Publish(new TicketPurchaseConfirmed(ticket1, orderId));
        await harness.Bus.Publish(new TicketPurchaseRejected(ticket2, "PaymentFailed", orderId));

        (await EventuallyAsync(() => harness.Published.Any<ReleaseConfirmedZoneSlot>(m => m.Context.Message.CorrelationId == ticket1 && m.Context.Message.ZoneId == zone1))).Should().BeTrue();
        (await harness.Published.Any<TicketRefunded>(m => m.Context.Message.CorrelationId == ticket1)).Should().BeTrue();
        (await harness.Published.Any<PurchaseOrderRejected>(m => m.Context.Message.Reason == "PaymentFailed")).Should().BeTrue();
    }
}
