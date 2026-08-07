namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Domain;

/// <summary>
/// Datos de partida para que "docker-compose up" deje la demo lista sin pasos manuales.
/// </summary>
public static class TicketingDbSeeder
{
    public static async Task SeedAsync(TicketingDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Events.AnyAsync(cancellationToken))
            return;

        var derbi = new Event(
            Guid.NewGuid(),
            "Real Madrid - Barcelona",
            "Estadio Santiago Bernabéu",
            DateTimeOffset.UtcNow.AddDays(7));
        derbi.AddTicketType(Guid.NewGuid(), "General", 45.00m, "Grada general, sin asiento numerado");
        derbi.AddTicketType(Guid.NewGuid(), "VIP", 180.00m, "Zona VIP con acceso a hospitality");

        var concierto = new Event(
            Guid.NewGuid(),
            "Concierto de apertura de temporada",
            "Estadio Metropolitano",
            DateTimeOffset.UtcNow.AddDays(14));
        concierto.AddTicketType(Guid.NewGuid(), "General", 30.00m, null);
        concierto.AddTicketType(Guid.NewGuid(), "Pista", 60.00m, "Acceso a pista, de pie");

        dbContext.Events.AddRange(derbi, concierto);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
