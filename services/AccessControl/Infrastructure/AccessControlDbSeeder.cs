namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Ya no siembra zonas fijas: cada evento aprovisiona las suyas al consumir `EventCreated`
/// (ver <see cref="Consumers.EventCreatedConsumer"/>) — solo queda aplicar migraciones.
/// </summary>
public static class AccessControlDbSeeder
{
    public static Task SeedAsync(AccessControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
