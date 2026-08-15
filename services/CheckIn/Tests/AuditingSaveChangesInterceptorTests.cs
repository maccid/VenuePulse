namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Domain;
using VenuePulse.CheckIn.Infrastructure;
using VenuePulse.Core.Data;

public class AuditingSaveChangesInterceptorTests
{
    private static CheckInDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CheckInDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditingSaveChangesInterceptor())
            .Options);

    [Fact]
    public async Task El_interceptor_de_auditoria_rellena_CreatedAtUtc_al_anadir()
    {
        await using var dbContext = CreateContext();
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ada Lovelace", 40.00m);

        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();

        ticket.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        ticket.UpdatedAtUtc.Should().Be(ticket.CreatedAtUtc);
    }

    [Fact]
    public async Task El_interceptor_de_auditoria_actualiza_solo_UpdatedAtUtc_al_modificar()
    {
        await using var dbContext = CreateContext();
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ada Lovelace", 40.00m);
        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();
        var createdAtUtc = ticket.CreatedAtUtc;

        ticket.MarkAwaitingPayment();
        ticket.MarkProcessingPayment();
        ticket.Confirm();
        await dbContext.SaveChangesAsync();

        ticket.CreatedAtUtc.Should().Be(createdAtUtc);
        ticket.UpdatedAtUtc.Should().BeOnOrAfter(createdAtUtc);
    }
}
