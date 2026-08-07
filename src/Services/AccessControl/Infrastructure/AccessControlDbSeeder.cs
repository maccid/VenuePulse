namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Domain;

public static class AccessControlDbSeeder
{
    public static async Task SeedAsync(AccessControlDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Zones.AnyAsync(cancellationToken))
            return;

        dbContext.Zones.AddRange(
            new Zone(Guid.NewGuid(), "Puerta Norte", 40),
            new Zone(Guid.NewGuid(), "Puerta Sur", 40),
            new Zone(Guid.NewGuid(), "Grada VIP", 15));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
