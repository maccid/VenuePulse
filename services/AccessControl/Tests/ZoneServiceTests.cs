namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Domain;
using VenuePulse.AccessControl.Infrastructure;
using VenuePulse.Core.Domain;

public class ZoneServiceTests
{
    private sealed class CapturingZoneOccupancyNotifier : IZoneOccupancyNotifier
    {
        public List<ZoneOccupancyDto> Notified { get; } = [];

        public Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken)
        {
            Notified.Add(occupancy);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Aprovisionar_zonas_notifica_el_estado_inicial_de_cada_una()
    {
        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var notifier = new CapturingZoneOccupancyNotifier();
        var service = new ZoneService(new ZoneRepository(dbContext), new OccupancySnapshotRepository(dbContext), notifier);
        var eventId = Guid.NewGuid();

        await service.ProvisionZonesForEventAsync(eventId, EventType.Match, CancellationToken.None);

        // Sin esta notificación inicial, una zona virgen (100% libre) queda invisible para
        // Ticketing hasta su primer cambio, e infla el % de ocupación agregado por evento
        // (ver ADR 0016) al excluir su aforo del denominador.
        notifier.Notified.Should().HaveCount(3);
        notifier.Notified.Should().OnlyContain(z => z.EventId == eventId && z.AvailableToSell == z.Capacity && z.ConfirmedSold == 0);
        notifier.Notified.Select(z => z.Name).Should().Contain("Grada VIP");
    }

    [Fact]
    public async Task Aprovisionar_zonas_de_un_concierto_usa_el_set_de_pista_en_vez_del_de_partido()
    {
        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var notifier = new CapturingZoneOccupancyNotifier();
        var service = new ZoneService(new ZoneRepository(dbContext), new OccupancySnapshotRepository(dbContext), notifier);
        var eventId = Guid.NewGuid();

        await service.ProvisionZonesForEventAsync(eventId, EventType.Concert, CancellationToken.None);

        // "Pista" tiene que existir con ese nombre exacto: el frontend fusiona zona+tipo de
        // entrada por coincidencia de nombre (el TicketType "Pista" del concierto), sin relación
        // real en el backend — si este nombre cambia, esa fusión deja de funcionar en silencio.
        var names = notifier.Notified.Select(z => z.Name).ToList();
        names.Should().Contain("Pista");
        names.Should().NotContain("Grada VIP");
        names.Should().NotContain("Puerta Norte");
    }

    [Fact]
    public async Task Reset_simulacion_limpia_los_snapshots_del_evento()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options;
        var eventId = Guid.NewGuid();

        // Contextos separados como en producción (scope por request): si reutilizamos el mismo
        // DbContext, las zonas quedan trackeadas del AddRange y el Attach de SaveAsync da conflicto.
        await using (var provisioningContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(provisioningContext), new OccupancySnapshotRepository(provisioningContext), new CapturingZoneOccupancyNotifier());
            await service.ProvisionZonesForEventAsync(eventId, EventType.Match, CancellationToken.None);
        }

        Guid zoneId;
        await using (var seedingContext = new AccessControlDbContext(options))
        {
            zoneId = (await new ZoneRepository(seedingContext).GetAllAsync(eventId, CancellationToken.None)).First().Id;
            var snapshotRepository = new OccupancySnapshotRepository(seedingContext);
            await snapshotRepository.AddRangeAsync(
                [new ZoneOccupancySnapshot(Guid.NewGuid(), eventId, zoneId, DateTimeOffset.UtcNow, 10, 8)],
                CancellationToken.None);
        }

        await using (var resetContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(resetContext), new OccupancySnapshotRepository(resetContext), new CapturingZoneOccupancyNotifier());
            await service.ResetSimulationAsync(eventId, CancellationToken.None);
        }

        // La predicción (Bloque 11) lee la serie temporal: si el reset la conserva, acumula
        // basura de corridas anteriores y degrada el ajuste (puntos fuera de la ventana a progress 0/1).
        await using var assertionContext = new AccessControlDbContext(options);
        var remaining = await new OccupancySnapshotRepository(assertionContext).GetForEventAsync(eventId, CancellationToken.None);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_las_zonas_del_evento_las_marca_y_notifica()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options;
        var eventId = Guid.NewGuid();

        // Contextos separados como en producción (scope por request): el AddRange deja las zonas
        // trackeadas y el Attach de SaveAsync sobre una instancia nueva (AsNoTracking) con la misma
        // clave chocaría — ver patrón del test de reset.
        await using (var provisioningContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(provisioningContext), new OccupancySnapshotRepository(provisioningContext), new CapturingZoneOccupancyNotifier());
            await service.ProvisionZonesForEventAsync(eventId, EventType.Match, CancellationToken.None);
        }

        var notifier = new CapturingZoneOccupancyNotifier();

        // Idempotente en producción: la redelivery de EventCancelled llega como otro mensaje con su
        // propio scope de request, así que cada "intento" usa un DbContext distinto — la segunda
        // llamada no debe fallar ni cambiar nada.
        await using (var firstCancellationContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(firstCancellationContext), new OccupancySnapshotRepository(firstCancellationContext), notifier);
            await service.CancelEventZonesAsync(eventId, CancellationToken.None);
        }

        await using (var secondCancellationContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(secondCancellationContext), new OccupancySnapshotRepository(secondCancellationContext), notifier);
            await service.CancelEventZonesAsync(eventId, CancellationToken.None);
        }

        await using (var assertionContext = new AccessControlDbContext(options))
        {
            var zones = await new ZoneRepository(assertionContext).GetAllAsync(eventId, CancellationToken.None);
            zones.Should().OnlyContain(z => z.Cancelled);
        }

        notifier.Notified.Should().HaveCount(6);
        notifier.Notified.Should().OnlyContain(z => z.EventId == eventId);
    }

    [Fact]
    public async Task Reservar_en_una_zona_cancelada_se_rechaza()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options;
        var eventId = Guid.NewGuid();

        await using (var provisioningContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(provisioningContext), new OccupancySnapshotRepository(provisioningContext), new CapturingZoneOccupancyNotifier());
            await service.ProvisionZonesForEventAsync(eventId, EventType.Match, CancellationToken.None);
        }

        await using (var cancellationContext = new AccessControlDbContext(options))
        {
            var service = new ZoneService(new ZoneRepository(cancellationContext), new OccupancySnapshotRepository(cancellationContext), new CapturingZoneOccupancyNotifier());
            await service.CancelEventZonesAsync(eventId, CancellationToken.None);
        }

        Guid zoneId;
        await using (var lookupContext = new AccessControlDbContext(options))
        {
            zoneId = (await new ZoneRepository(lookupContext).GetAllAsync(eventId, CancellationToken.None)).First().Id;
        }

        await using var reservationContext = new AccessControlDbContext(options);
        var reservationService = new ZoneService(new ZoneRepository(reservationContext), new OccupancySnapshotRepository(reservationContext), new CapturingZoneOccupancyNotifier());

        var reserved = await reservationService.TryReserveSlotAsync(zoneId, CancellationToken.None);

        reserved.Should().BeFalse();
    }

    [Fact]
    public async Task Liberar_una_entrada_confirmada_decrementa_y_notifica()
    {
        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var notifier = new CapturingZoneOccupancyNotifier();
        var service = new ZoneService(new ZoneRepository(dbContext), new OccupancySnapshotRepository(dbContext), notifier);
        var eventId = Guid.NewGuid();
        await service.ProvisionZonesForEventAsync(eventId, EventType.Match, CancellationToken.None);
        var zoneId = (await new ZoneRepository(dbContext).GetAllAsync(eventId, CancellationToken.None)).First().Id;
        await service.TryReserveSlotAsync(zoneId, CancellationToken.None);
        await service.ConfirmSlotAsync(zoneId, CancellationToken.None);
        notifier.Notified.Clear();

        await service.ReleaseConfirmedSlotAsync(zoneId, CancellationToken.None);

        var zone = await new ZoneRepository(dbContext).GetByIdAsync(zoneId, CancellationToken.None);
        zone!.ConfirmedSold.Should().Be(0);
        notifier.Notified.Should().ContainSingle(z => z.ZoneId == zoneId && z.ConfirmedSold == 0);
    }
}
