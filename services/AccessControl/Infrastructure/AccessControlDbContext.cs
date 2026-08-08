namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Domain;

public sealed class AccessControlDbContext(DbContextOptions<AccessControlDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones => Set<Zone>();

    public DbSet<ZoneOccupancySnapshot> OccupancySnapshots => Set<ZoneOccupancySnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zone>(builder =>
        {
            builder.ToTable("zones");
            builder.HasKey(z => z.Id);
            builder.Property(z => z.EventId).IsRequired();
            builder.Property(z => z.Name).HasMaxLength(120).IsRequired();
            builder.Property(z => z.Capacity).IsRequired();
            builder.Property(z => z.Reserved).IsRequired();
            builder.Property(z => z.ConfirmedSold).IsRequired();
            builder.Property(z => z.CheckedIn).IsRequired();
            builder.HasIndex(z => z.EventId);
            builder.HasIndex(z => new { z.EventId, z.Name }).IsUnique();
            builder.Ignore(z => z.DomainEvents);
        });

        modelBuilder.Entity<ZoneOccupancySnapshot>(builder =>
        {
            builder.ToTable("occupancy_snapshots");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.EventId).IsRequired();
            builder.Property(s => s.ZoneId).IsRequired();
            builder.Property(s => s.RecordedAtUtc).IsRequired();
            builder.Property(s => s.ConfirmedSold).IsRequired();
            builder.Property(s => s.CheckedIn).IsRequired();
            builder.HasIndex(s => new { s.EventId, s.RecordedAtUtc });
            builder.Ignore(s => s.DomainEvents);
        });
    }
}
