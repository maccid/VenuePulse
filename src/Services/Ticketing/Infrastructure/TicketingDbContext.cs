namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Domain;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(builder =>
        {
            builder.ToTable("events");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
            builder.Property(e => e.VenueName).HasMaxLength(200).IsRequired();
            builder.Property(e => e.StartsAtUtc).IsRequired();
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
    }
}
