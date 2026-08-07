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

    [Fact]
    public async Task Camino_feliz_confirma_la_compra()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId, SimulateFailure: false));
        (await harness.Published.Any<ReserveZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId, SimulateFailure: false));
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotConfirmed(ticketId, zoneId));
        (await harness.Published.Any<TicketPurchaseConfirmed>()).Should().BeTrue();

        // SetCompletedWhenFinalized() borra la instancia del repositorio al finalizar —
        // por eso la prueba de "llegó a Completed" es la publicación de TicketPurchaseConfirmed
        // (arriba), no una consulta de existencia contra un estado final ya eliminado.
    }

    [Fact]
    public async Task Camino_de_compensacion_libera_el_aforo_reservado()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId, SimulateFailure: true));
        await harness.Bus.Publish(new ZoneSlotReserved(ticketId, zoneId, SimulateFailure: true));
        (await harness.Published.Any<ReleaseZoneSlot>()).Should().BeTrue();

        await harness.Bus.Publish(new ZoneSlotReleased(ticketId, zoneId));
        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeTrue();
    }

    [Fact]
    public async Task Zona_llena_rechaza_directamente_sin_llegar_a_reservar_pago()
    {
        var (provider, harness) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId, SimulateFailure: false));
        await harness.Bus.Publish(new ZoneSlotReservationFailed(ticketId, zoneId, "ZoneFull"));

        (await harness.Published.Any<TicketPurchaseRejected>()).Should().BeTrue();
        (await harness.Published.Any<ConfirmZoneSlot>()).Should().BeFalse();
    }
}
