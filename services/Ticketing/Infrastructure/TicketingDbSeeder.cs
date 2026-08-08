namespace VenuePulse.Ticketing.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.Core.Messaging.Contracts;
using Event = VenuePulse.Ticketing.Domain.Event;

/// <summary>
/// Datos de partida para que "docker-compose up" deje la demo lista sin pasos manuales.
///
/// Los dos eventos comparten el MISMO recinto físico (Estadio Santiago Bernabéu) a propósito
/// — es una demo de un único recinto — pero cada uno tiene su propio aforo por zona: al
/// sembrarse aquí, cada `Event` publica `EventCreated` (Add -> Publish -> SaveChanges, mismo
/// patrón que <see cref="VenuePulse.CheckIn.Application.Commands.PurchaseTicketCommand"/>) y
/// `AccessControl` aprovisiona sus 3 zonas fijas ya vinculadas a ese `EventId` (ver
/// .ia/KNOWN-ISSUES.md).
/// </summary>
public static class TicketingDbSeeder
{
    private const string Venue = "Estadio Santiago Bernabéu";

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
            "Real Madrid - Barcelona",
            Venue,
            DateTimeOffset.UtcNow.AddDays(7));
        derbi.AddTicketType(Guid.NewGuid(), "General", 45.00m, "Grada general, sin asiento numerado");
        derbi.AddTicketType(Guid.NewGuid(), "VIP", 180.00m, "Zona VIP con acceso a hospitality");

        var concierto = new Event(
            Guid.NewGuid(),
            "Concierto de apertura de temporada",
            Venue,
            DateTimeOffset.UtcNow.AddDays(14));
        concierto.AddTicketType(Guid.NewGuid(), "General", 30.00m, null);
        concierto.AddTicketType(Guid.NewGuid(), "Pista", 60.00m, "Acceso a pista, de pie");

        dbContext.Events.AddRange(derbi, concierto);

        await publishEndpoint.Publish(new EventCreated(derbi.Id, derbi.Name, derbi.VenueName, derbi.StartsAtUtc), cancellationToken);
        await publishEndpoint.Publish(new EventCreated(concierto.Id, concierto.Name, concierto.VenueName, concierto.StartsAtUtc), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
