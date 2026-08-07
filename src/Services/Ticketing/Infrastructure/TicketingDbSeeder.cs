namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Domain;

/// <summary>
/// Datos de partida para que "docker-compose up" deje la demo lista sin pasos manuales.
///
/// Los dos eventos comparten el MISMO recinto a propósito: `AccessControl` modela un único
/// recinto físico con zonas/puertas fijas (sin `EventId` en `Zone`, ver ADR de alcance) — el
/// aforo es una propiedad de la infraestructura física, reutilizada entre eventos que no
/// coinciden en el tiempo, no algo que se reparta por evento. Sembrar dos "venueName"
/// distintos sería inconsistente con ese modelo (dos estadios no pueden compartir el aforo de
/// sus puertas).
/// </summary>
public static class TicketingDbSeeder
{
    private const string Venue = "Estadio Santiago Bernabéu";

    public static async Task SeedAsync(TicketingDbContext dbContext, CancellationToken cancellationToken = default)
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
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
