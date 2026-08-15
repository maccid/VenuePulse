namespace VenuePulse.Ticketing.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Domain;
using VenuePulse.Ticketing.Infrastructure;
using VenuePulse.Ticketing.Infrastructure.Consumers;
using Event = VenuePulse.Ticketing.Domain.Event;

public class ZoneOccupancyChangedConsumerTests
{
    private sealed class CapturingPricingNotifier : IPricingNotifier
    {
        public Guid? LastEventId { get; private set; }
        public IReadOnlyCollection<TicketTypeDto>? LastTicketTypes { get; private set; }

        public Task NotifyPricingChangedAsync(
            Guid eventId, IReadOnlyCollection<TicketTypeDto> ticketTypes, CancellationToken cancellationToken)
        {
            LastEventId = eventId;
            LastTicketTypes = ticketTypes;
            return Task.CompletedTask;
        }
    }

    private static async Task<(ServiceProvider Provider, ITestHarness Harness, string DatabaseName)> StartHarnessAsync()
    {
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TicketingDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IZoneDemandRepository, ZoneDemandRepository>();
        services.AddScoped<ITicketingService, TicketingService>();
        services.AddSingleton<IPricingNotifier, CapturingPricingNotifier>();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<ZoneOccupancyChangedConsumer>();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return (provider, harness, databaseName);
    }

    private static async Task WaitUntilConsumedAsync(ITestHarness harness, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (harness.Consumed.Select<ZoneOccupancyChanged>().Count() < expectedCount && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }

    private static async Task<Event> SeedEventAsync(string databaseName, decimal price = 100m)
    {
        await using var dbContext = new TicketingDbContext(
            new DbContextOptionsBuilder<TicketingDbContext>().UseInMemoryDatabase(databaseName).Options);

        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));
        @event.AddTicketType(Guid.NewGuid(), "General", price, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();
        return @event;
    }

    [Fact]
    public async Task Actualiza_la_proyeccion_de_aforo_y_notifica_el_precio_recalculado()
    {
        var (provider, harness, databaseName) = await StartHarnessAsync();
        await using var _ = provider;
        var @event = await SeedEventAsync(databaseName);
        var zoneId = Guid.NewGuid();

        var message = new ZoneOccupancyChanged(zoneId, @event.Id, Capacity: 100, AvailableToSell: 5, ConfirmedSold: 95, CheckedIn: 0);
        await harness.Bus.Publish(message);

        await WaitUntilConsumedAsync(harness, expectedCount: 1);
        harness.Consumed.Select<ZoneOccupancyChanged>().Count().Should().Be(1);

        await using var dbContext = new TicketingDbContext(
            new DbContextOptionsBuilder<TicketingDbContext>().UseInMemoryDatabase(databaseName).Options);
        var snapshot = await dbContext.ZoneDemandSnapshots.AsNoTracking().SingleAsync(s => s.ZoneId == zoneId);
        snapshot.EventId.Should().Be(@event.Id);
        snapshot.AvailableToSell.Should().Be(5);

        var notifier = (CapturingPricingNotifier)provider.GetRequiredService<IPricingNotifier>();
        notifier.LastEventId.Should().Be(@event.Id);
        notifier.LastTicketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.EffectivePrice == 130m && t.PriceTier == DynamicPricing.TierUltimas);
    }

    [Fact]
    public async Task Procesar_el_mismo_snapshot_dos_veces_deja_el_mismo_resultado()
    {
        var (provider, harness, databaseName) = await StartHarnessAsync();
        await using var _ = provider;
        var @event = await SeedEventAsync(databaseName);
        var zoneId = Guid.NewGuid();

        var message = new ZoneOccupancyChanged(zoneId, @event.Id, Capacity: 100, AvailableToSell: 40, ConfirmedSold: 60, CheckedIn: 0);
        await harness.Bus.Publish(message);
        await harness.Bus.Publish(message);

        await WaitUntilConsumedAsync(harness, expectedCount: 2);

        await using var dbContext = new TicketingDbContext(
            new DbContextOptionsBuilder<TicketingDbContext>().UseInMemoryDatabase(databaseName).Options);
        var count = await dbContext.ZoneDemandSnapshots.CountAsync(s => s.ZoneId == zoneId);
        count.Should().Be(1);
    }
}
