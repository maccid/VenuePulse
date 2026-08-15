namespace VenuePulse.CheckIn.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Domain;

public sealed class CheckInDbContext(DbContextOptions<CheckInDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

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
            builder.HasIndex(t => t.GroupId);
        });

        modelBuilder.Entity<PurchaseOrder>(builder =>
        {
            builder.ToTable("purchase_orders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Holder).HasMaxLength(200).IsRequired();
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(o => o.RejectionReason).HasMaxLength(200);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
