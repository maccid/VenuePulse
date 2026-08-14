namespace VenuePulse.Ticketing.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Domain;
using Event = VenuePulse.Ticketing.Domain.Event;

/// <summary>
/// Datos de partida para que "docker-compose up" deje la demo lista sin pasos manuales.
///
/// Los dos eventos comparten el MISMO recinto físico (Estadio Central) a propósito
/// — es una demo de un único recinto — pero cada uno tiene su propio aforo por zona: al
/// sembrarse aquí, cada `Event` publica `EventCreated` (Add -> Publish -> SaveChanges, mismo
/// patrón que <see cref="VenuePulse.CheckIn.Application.Commands.PurchaseTicketCommand"/>) y
/// `AccessControl` aprovisiona sus zonas fijas (4 en partido, 6 en concierto — ver ADR 0025/0026)
/// ya vinculadas a ese `EventId` (ver .ia/KNOWN-ISSUES.md).
/// </summary>
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

        // El derbi es el ejemplo vivo de EventRelevance.Special (multiplicador de precio, ver
        // ADR 0019) — un partido de máxima rivalidad es justo el caso de uso que motiva el selector.
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
