namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Domain;
using VenuePulse.AccessControl.Infrastructure.Inbox;

public sealed class AccessControlDbContext(DbContextOptions<AccessControlDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones => Set<Zone>();

    public DbSet<ZoneOccupancySnapshot> OccupancySnapshots => Set<ZoneOccupancySnapshot>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

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
            builder.Property(z => z.Cancelled).IsRequired();
            builder.HasIndex(z => z.EventId);
            builder.HasIndex(z => new { z.EventId, z.Name }).IsUnique();

            // Token de concurrencia optimista: Zone no lo expone como propiedad C# (el dominio es
            // persistence-ignorant, ver .ia/RULES.md) — vive solo aquí como shadow property. Cierra
            // el lost-update entre Reserve/Release/Confirm concurrentes sobre la misma zona, descartado
            // como fuera de alcance en ADR 0027 y resuelto aquí como su propio fix.
            builder.Property<byte[]>("RowVersion").IsRowVersion();
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
        });

        modelBuilder.Entity<ProcessedMessage>(builder =>
        {
            builder.ToTable("processed_messages");
            builder.HasKey(p => p.EventId);
            builder.Property(p => p.MessageType).HasMaxLength(200).IsRequired();
            builder.Property(p => p.ProcessedAtUtc).IsRequired();
        });
    }
}
