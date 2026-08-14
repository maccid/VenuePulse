namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Domain;
using VenuePulse.CheckIn.Infrastructure;
using VenuePulse.Core.Data;

/// <summary>
/// Ejercita <see cref="AuditingSaveChangesInterceptor"/> (Bloque 4, ver ADR 0011) contra
/// CheckInDbContext con el proveedor InMemory de EF Core — rápido y determinista, sin necesitar
/// Postgres real (eso ya lo cubren las IntegrationTests con Testcontainers).
/// </summary>
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
