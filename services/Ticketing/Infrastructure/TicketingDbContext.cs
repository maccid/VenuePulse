namespace VenuePulse.Ticketing.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.Core.Domain;
using VenuePulse.Ticketing.Domain;
using Event = VenuePulse.Ticketing.Domain.Event;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<ZoneDemandSnapshot> ZoneDemandSnapshots => Set<ZoneDemandSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(builder =>
        {
            builder.ToTable("events");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
            builder.Property(e => e.VenueName).HasMaxLength(200).IsRequired();
            builder.Property(e => e.StartsAtUtc).IsRequired();
            builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EventType.Match);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EventStatus.OnSale);
            builder.Property(e => e.Relevance).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EventRelevance.Standard);
            builder.Ignore(e => e.DomainEvents);

            builder.OwnsMany(e => e.TicketTypes, tt =>
            {
                tt.ToTable("ticket_types");
                tt.WithOwner().HasForeignKey(t => t.EventId);
                tt.HasKey(t => t.Id);
                tt.Property(t => t.Name).HasMaxLength(120).IsRequired();
                tt.Property(t => t.Price).HasColumnType("decimal(10,2)");
                tt.Property(t => t.Description).HasMaxLength(500);
                tt.Ignore(t => t.DomainEvents);
            });

            builder.Navigation(e => e.TicketTypes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ZoneDemandSnapshot>(builder =>
        {
            builder.ToTable("zone_demand_snapshots");
            builder.HasKey(s => s.ZoneId);
        });

        // Tablas del Outbox/Inbox transaccional de MassTransit (ver ADR 0002).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
