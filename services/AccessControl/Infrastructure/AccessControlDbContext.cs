namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Domain;

public sealed class AccessControlDbContext(DbContextOptions<AccessControlDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones => Set<Zone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zone>(builder =>
        {
            builder.ToTable("zones");
            builder.HasKey(z => z.Id);
            builder.Property(z => z.Name).HasMaxLength(120).IsRequired();
            builder.Property(z => z.Capacity).IsRequired();
            builder.Property(z => z.Reserved).IsRequired();
            builder.Property(z => z.ConfirmedSold).IsRequired();
            builder.Property(z => z.CheckedIn).IsRequired();
            builder.Ignore(z => z.DomainEvents);
        });
    }
}
