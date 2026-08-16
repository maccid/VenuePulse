namespace VenuePulse.Ticketing.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Domain;
using VenuePulse.Ticketing.Infrastructure;
using Event = VenuePulse.Ticketing.Domain.Event;

public class TicketingServiceTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness, ITicketingService Service, string DatabaseName)> StartAsync()
    {
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TicketingDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IZoneDemandRepository, ZoneDemandRepository>();
        services.AddScoped<ITicketingService, TicketingService>();

        services.AddMassTransitTestHarness(x => x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context)));

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return (provider, harness, provider.GetRequiredService<ITicketingService>(), databaseName);
    }

    private static TicketingDbContext OpenContext(string databaseName) =>
        new(new DbContextOptionsBuilder<TicketingDbContext>().UseInMemoryDatabase(databaseName).Options);

    private static async Task<Event> SeedEventAsync(string databaseName, decimal price = 100m, EventRelevance relevance = EventRelevance.Standard)
    {
        await using var dbContext = OpenContext(databaseName);

        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1), EventType.Match, relevance);
        @event.AddTicketType(Guid.NewGuid(), "General", price, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();
        return @event;
    }

    [Fact]
    public async Task Sin_ninguna_zona_notificada_el_precio_es_el_de_catalogo()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Price == 100m && t.BasePrice == 100m && t.EffectivePrice == 100m && t.PriceTier == DynamicPricing.TierNormal);
    }

    [Fact]
    public async Task Con_el_evento_casi_agotado_el_precio_efectivo_sube()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);

        var demandRepository = new ZoneDemandRepository(OpenContext(databaseName));
        await demandRepository.UpsertAsync(Guid.NewGuid(), @event.Id, "General", capacity: 100, availableToSell: 5, confirmedSold: 95, checkedIn: 0, CancellationToken.None);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Price == 100m && t.BasePrice == 100m && t.EffectivePrice == 130m && t.PriceTier == DynamicPricing.TierUltimas);
    }

    [Fact]
    public async Task Cada_tipo_de_entrada_recibe_el_tier_de_su_propia_zona_no_el_agregado_del_evento()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;

        await using var dbContext = OpenContext(databaseName);
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1), EventType.Match, EventRelevance.Standard);
        @event.AddTicketType(Guid.NewGuid(), "Grada VIP Norte", 90m, null);
        @event.AddTicketType(Guid.NewGuid(), "General", 40m, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();

        var demandRepository = new ZoneDemandRepository(OpenContext(databaseName));
        await demandRepository.UpsertAsync(Guid.NewGuid(), @event.Id, "Grada VIP Norte", capacity: 8, availableToSell: 0, confirmedSold: 8, checkedIn: 0, CancellationToken.None);
        await demandRepository.UpsertAsync(Guid.NewGuid(), @event.Id, "Grada Este", capacity: 40, availableToSell: 39, confirmedSold: 1, checkedIn: 0, CancellationToken.None);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().Contain(t => t.Name == "Grada VIP Norte" && t.PriceTier == DynamicPricing.TierUltimas);
        ticketTypes.Should().Contain(t => t.Name == "General" && t.PriceTier == DynamicPricing.TierBaja);
    }

    [Fact]
    public async Task Una_zona_sin_nombre_exacto_fusiona_por_contencion_con_el_tipo_de_entrada()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;

        await using var dbContext = OpenContext(databaseName);
        var @event = new Event(Guid.NewGuid(), "Gira mundial", "Estadio", DateTimeOffset.UtcNow.AddDays(1), EventType.Concert, EventRelevance.Standard);
        @event.AddTicketType(Guid.NewGuid(), "VIP", 90m, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();

        var demandRepository = new ZoneDemandRepository(OpenContext(databaseName));
        await demandRepository.UpsertAsync(Guid.NewGuid(), @event.Id, "Grada VIP Norte", capacity: 10, availableToSell: 1, confirmedSold: 9, checkedIn: 0, CancellationToken.None);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Name == "VIP" && t.PriceTier == DynamicPricing.TierUltimas);
    }

    [Fact]
    public async Task Un_evento_especial_multiplica_el_precio_base_antes_de_la_demanda()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName, price: 100m, relevance: EventRelevance.Special);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Price == 100m && t.BasePrice == 150m && t.EffectivePrice == 150m && t.PriceTier == DynamicPricing.TierNormal);
    }

    [Fact]
    public async Task Crear_un_evento_de_partido_lo_deja_en_venta_y_publica_EventCreated()
    {
        var (provider, harness, service, _) = await StartAsync();
        await using var _p = provider;

        var created = await service.CreateEventAsync(
            new CreateEventRequest("Amistoso de pretemporada", DateTimeOffset.UtcNow.AddDays(3), EventType.Match, EventRelevance.Standard),
            CancellationToken.None);

        created.Status.Should().Be(EventStatus.OnSale);
        created.VenueName.Should().Be("Estadio Central");
        (await harness.Published.Any<EventCreated>(m => m.Context.Message.Id == created.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Crear_un_evento_de_partido_anade_general_y_gradas_vip_por_defecto()
    {
        var (provider, _, service, _) = await StartAsync();
        await using var _p = provider;

        var created = await service.CreateEventAsync(
            new CreateEventRequest("Amistoso de pretemporada", DateTimeOffset.UtcNow.AddDays(3), EventType.Match, EventRelevance.Standard),
            CancellationToken.None);

        var ticketTypes = await service.GetTicketTypesAsync(created.Id, CancellationToken.None);

        ticketTypes.Should().HaveCount(3);
        ticketTypes.Should().Contain(t => t.Name == "General" && t.Price == 40.00m);
        ticketTypes.Should().Contain(t => t.Name == "Grada VIP Norte" && t.Price == 90.00m);
        ticketTypes.Should().Contain(t => t.Name == "Grada VIP Sur" && t.Price == 90.00m);
    }

    [Fact]
    public async Task Crear_un_evento_de_concierto_anade_los_seis_tipos_por_defecto()
    {
        var (provider, _, service, _) = await StartAsync();
        await using var _p = provider;

        var created = await service.CreateEventAsync(
            new CreateEventRequest("Gira mundial", DateTimeOffset.UtcNow.AddDays(3), EventType.Concert, EventRelevance.Standard),
            CancellationToken.None);

        var ticketTypes = await service.GetTicketTypesAsync(created.Id, CancellationToken.None);

        ticketTypes.Should().HaveCount(6);
        ticketTypes.Should().Contain(t => t.Name == "Grada 1" && t.Price == 25.00m);
        ticketTypes.Should().Contain(t => t.Name == "Grada 2" && t.Price == 25.00m);
        ticketTypes.Should().Contain(t => t.Name == "Grada 3" && t.Price == 25.00m);
        ticketTypes.Should().Contain(t => t.Name == "Pista" && t.Price == 60.00m);
        ticketTypes.Should().Contain(t => t.Name == "VIP 1" && t.Price == 90.00m);
        ticketTypes.Should().Contain(t => t.Name == "VIP 2" && t.Price == 90.00m);
    }

    [Fact]
    public async Task Cerrar_venta_de_un_evento_en_venta_tiene_exito()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);

        var result = await service.CloseSalesAsync(@event.Id, CancellationToken.None);

        result.Should().Be(EventLifecycleResult.Success);
        var events = await service.GetEventsAsync(CancellationToken.None);
        events.Should().ContainSingle(e => e.Id == @event.Id && e.Status == EventStatus.SalesClosed);
    }

    [Fact]
    public async Task Cerrar_venta_de_un_evento_que_no_existe_devuelve_NotFound()
    {
        var (provider, _, service, _) = await StartAsync();
        await using var _p = provider;

        var result = await service.CloseSalesAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().Be(EventLifecycleResult.NotFound);
    }

    [Fact]
    public async Task Cerrar_venta_dos_veces_devuelve_InvalidState()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);
        await service.CloseSalesAsync(@event.Id, CancellationToken.None);

        var result = await service.CloseSalesAsync(@event.Id, CancellationToken.None);

        result.Should().Be(EventLifecycleResult.InvalidState);
    }

    [Fact]
    public async Task Cancelar_un_evento_en_venta_publica_EventCancelled()
    {
        var (provider, harness, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);

        var result = await service.CancelEventAsync(@event.Id, CancellationToken.None);

        result.Should().Be(EventLifecycleResult.Success);
        (await harness.Published.Any<EventCancelled>(m => m.Context.Message.Id == @event.Id)).Should().BeTrue();
        var events = await service.GetEventsAsync(CancellationToken.None);
        events.Should().ContainSingle(e => e.Id == @event.Id && e.Status == EventStatus.Cancelled);
    }

    [Fact]
    public async Task No_se_puede_cancelar_un_evento_con_la_venta_ya_cerrada()
    {
        var (provider, _, service, databaseName) = await StartAsync();
        await using var _p = provider;
        var @event = await SeedEventAsync(databaseName);
        await service.CloseSalesAsync(@event.Id, CancellationToken.None);

        var result = await service.CancelEventAsync(@event.Id, CancellationToken.None);

        result.Should().Be(EventLifecycleResult.InvalidState);
    }
}
