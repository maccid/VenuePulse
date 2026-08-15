namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;

public static class AccessControlDbSeeder
{
    public static Task SeedAsync(AccessControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
