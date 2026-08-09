namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Domain;
using VenuePulse.AccessControl.Infrastructure;
using VenuePulse.AccessControl.Infrastructure.Consumers;
using VenuePulse.AccessControl.Infrastructure.Inbox;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Inbox real de AccessControl (Bloque 3, ver ADR 0015) con el harness in-memory de MassTransit
/// (sin broker real, igual que TicketPurchaseSagaTests en CheckIn): una redelivery del mismo
/// mensaje (mismo EventId) no debe duplicar el efecto de negocio, solo la primera entrega debe
/// procesarse — la segunda queda descartada por el filtro antes de llegar al consumer.
/// </summary>
public class InboxDeduplicationFilterTests
{
    private sealed class NoopZoneOccupancyNotifier : IZoneOccupancyNotifier
    {
        public Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static async Task<(ServiceProvider Provider, ITestHarness Harness, string DatabaseName)> StartHarnessAsync()
    {
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AccessControlDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<IOccupancySnapshotRepository, OccupancySnapshotRepository>();
        services.AddSingleton<IZoneOccupancyNotifier, NoopZoneOccupancyNotifier>();
        services.AddScoped(typeof(InboxDeduplicationFilter<>));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ReserveZoneSlotConsumer>();
            x.UsingInMemory((context, cfg) =>
            {
                cfg.UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context);
                cfg.ConfigureEndpoints(context);
            });
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return (provider, harness, databaseName);
    }

    private static async Task<Zone> SeedZoneAsync(ServiceProvider provider, string databaseName, int capacity = 10)
    {
        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options);

        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity);
        dbContext.Zones.Add(zone);
        await dbContext.SaveChangesAsync();
        return zone;
    }

    private static async Task WaitUntilConsumedAsync(ITestHarness harness, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (harness.Consumed.Select<ReserveZoneSlot>().Count() < expectedCount && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }

    [Fact]
    public async Task Una_redelivery_del_mismo_mensaje_no_duplica_la_reserva()
    {
        var (provider, harness, databaseName) = await StartHarnessAsync();
        await using var _ = provider;
        var zone = await SeedZoneAsync(provider, databaseName);

        var command = new ReserveZoneSlot(Guid.NewGuid(), zone.Id, SimulateFailure: false);

        await harness.Bus.Publish(command);
        await harness.Bus.Publish(command); // misma instancia => mismo EventId => redelivery simulada

        await WaitUntilConsumedAsync(harness, expectedCount: 2);

        harness.Published.Select<ZoneSlotReserved>().Count().Should().Be(1);

        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options);
        (await dbContext.Zones.AsNoTracking().SingleAsync(z => z.Id == zone.Id)).Reserved.Should().Be(1);
        (await dbContext.ProcessedMessages.CountAsync(p => p.EventId == command.EventId)).Should().Be(1);
    }

    [Fact]
    public async Task Dos_mensajes_distintos_se_procesan_ambos()
    {
        var (provider, harness, databaseName) = await StartHarnessAsync();
        await using var _ = provider;
        var zone = await SeedZoneAsync(provider, databaseName);

        await harness.Bus.Publish(new ReserveZoneSlot(Guid.NewGuid(), zone.Id, SimulateFailure: false));
        await harness.Bus.Publish(new ReserveZoneSlot(Guid.NewGuid(), zone.Id, SimulateFailure: false));

        await WaitUntilConsumedAsync(harness, expectedCount: 2);

        harness.Published.Select<ZoneSlotReserved>().Count().Should().Be(2);

        await using var dbContext = new AccessControlDbContext(
            new DbContextOptionsBuilder<AccessControlDbContext>().UseInMemoryDatabase(databaseName).Options);
        (await dbContext.Zones.AsNoTracking().SingleAsync(z => z.Id == zone.Id)).Reserved.Should().Be(2);
    }
}
