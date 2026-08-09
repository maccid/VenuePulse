namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Domain;
using VenuePulse.CheckIn.Infrastructure;
using VenuePulse.Core.Data;

/// <summary>
/// Ejercita las utilidades nuevas de VenuePulse.Core (Bloque 4, ver ADR 0011) contra
/// CheckInDbContext con el proveedor InMemory de EF Core — rápido y determinista, sin necesitar
/// Postgres real (eso ya lo cubren las IntegrationTests con Testcontainers).
/// </summary>
public class CoreSharedPatternsTests
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
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ada Lovelace");

        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();

        ticket.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        ticket.UpdatedAtUtc.Should().Be(ticket.CreatedAtUtc);
    }

    [Fact]
    public async Task El_interceptor_de_auditoria_actualiza_solo_UpdatedAtUtc_al_modificar()
    {
        await using var dbContext = CreateContext();
        var ticket = new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ada Lovelace");
        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();
        var createdAtUtc = ticket.CreatedAtUtc;

        ticket.MarkAwaitingPayment();
        ticket.Confirm();
        await dbContext.SaveChangesAsync();

        ticket.CreatedAtUtc.Should().Be(createdAtUtc);
        ticket.UpdatedAtUtc.Should().BeOnOrAfter(createdAtUtc);
    }

    [Fact]
    public async Task La_paginacion_generica_devuelve_la_pagina_pedida_y_el_total_real()
    {
        await using var dbContext = CreateContext();
        for (var i = 0; i < 5; i++)
            await dbContext.Tickets.AddAsync(new Ticket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"Fan {i}"));
        await dbContext.SaveChangesAsync();

        var page = await dbContext.Tickets.OrderBy(t => t.Holder).ToPagedResultAsync(pageNumber: 2, pageSize: 2, CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Items.Select(t => t.Holder).Should().Equal("Fan 2", "Fan 3");
    }
}
