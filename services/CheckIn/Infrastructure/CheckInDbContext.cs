namespace VenuePulse.CheckIn.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Domain;

public sealed class CheckInDbContext(DbContextOptions<CheckInDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.ToTable("tickets");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Holder).HasMaxLength(200).IsRequired();
            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(t => t.RejectionReason).HasMaxLength(200);
            builder.Property(t => t.Price).HasColumnType("decimal(10,2)");
            builder.Ignore(t => t.DomainEvents);
        });

        // Tablas del Outbox/Inbox transaccional de MassTransit (ver ADR 0002).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
