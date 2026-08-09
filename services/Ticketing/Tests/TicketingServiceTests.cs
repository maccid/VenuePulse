namespace VenuePulse.Ticketing.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Domain;
using VenuePulse.Ticketing.Infrastructure;

public class TicketingServiceTests
{
    private static TicketingDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<TicketingDbContext>().UseInMemoryDatabase(databaseName).Options);

    [Fact]
    public async Task Sin_ninguna_zona_notificada_el_precio_es_el_de_catalogo()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateContext(databaseName);

        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));
        @event.AddTicketType(Guid.NewGuid(), "General", 100m, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();

        var service = new TicketingService(new EventRepository(dbContext), new ZoneDemandRepository(dbContext));

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Price == 100m && t.EffectivePrice == 100m && t.PriceTier == DynamicPricing.TierNormal);
    }

    [Fact]
    public async Task Con_el_evento_casi_agotado_el_precio_efectivo_sube()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateContext(databaseName);

        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));
        @event.AddTicketType(Guid.NewGuid(), "General", 100m, null);
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync();

        var demandRepository = new ZoneDemandRepository(dbContext);
        // Aforo total 100, disponible 5 => 95% ocupado.
        await demandRepository.UpsertAsync(Guid.NewGuid(), @event.Id, capacity: 100, availableToSell: 5, confirmedSold: 95, checkedIn: 0, CancellationToken.None);

        var service = new TicketingService(new EventRepository(dbContext), demandRepository);

        var ticketTypes = await service.GetTicketTypesAsync(@event.Id, CancellationToken.None);

        ticketTypes.Should().ContainSingle().Which.Should().Match<TicketTypeDto>(
            t => t.Price == 100m && t.EffectivePrice == 130m && t.PriceTier == DynamicPricing.TierUltimas);
    }
}
