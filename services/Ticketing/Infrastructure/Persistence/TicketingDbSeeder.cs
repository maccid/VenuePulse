namespace VenuePulse.Ticketing.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Domain;
using Event = VenuePulse.Ticketing.Domain.Event;

public static class TicketingDbSeeder
{
    private const string Venue = "Estadio Central";

    public static async Task SeedAsync(
        TicketingDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Events.AnyAsync(cancellationToken))
            return;

        var derbi = new Event(
            Guid.NewGuid(),
            "Derbi de la ciudad",
            Venue,
            DateTimeOffset.UtcNow.AddDays(7),
            EventType.Match,
            EventRelevance.Special);
        foreach (var (name, price, description) in DefaultTicketTypeCatalog.For(EventType.Match))
            derbi.AddTicketType(Guid.NewGuid(), name, price, description);

        var concierto = new Event(
            Guid.NewGuid(),
            "Concierto de apertura de temporada",
            Venue,
            DateTimeOffset.UtcNow.AddDays(14),
            EventType.Concert);
        foreach (var (name, price, description) in DefaultTicketTypeCatalog.For(EventType.Concert))
            concierto.AddTicketType(Guid.NewGuid(), name, price, description);

        dbContext.Events.AddRange(derbi, concierto);

        await publishEndpoint.Publish(new EventCreated(derbi.Id, derbi.Name, derbi.VenueName, derbi.StartsAtUtc, derbi.Type), cancellationToken);
        await publishEndpoint.Publish(new EventCreated(concierto.Id, concierto.Name, concierto.VenueName, concierto.StartsAtUtc, concierto.Type), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
