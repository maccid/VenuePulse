namespace VenuePulse.CheckIn.IntegrationTests;

using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using VenuePulse.CheckIn.Application;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// La pieza más contundente del repo: prueba la Saga de extremo a extremo contra
/// infraestructura REAL (Postgres, RabbitMQ, MongoDB vía Testcontainers), no mocks. AccessControl
/// no se levanta como servicio — un consumidor mínimo dentro del propio test hace de doble para
/// simular sus respuestas, manteniendo el test rápido sin perder realismo de infraestructura.
/// Cubre explícitamente el camino feliz Y el camino de compensación (ver ADR 0001).
/// Timeout generoso (90s): arrancar 3 contenedores + el host completo de ASP.NET Core en una
/// máquina compartida bajo carga puede tardar más que en un runner de CI dedicado.
///
/// NOTA (ver .ia/KNOWN-ISSUES.md): "Bizum_compensa_y_rechaza_la_entrada" se ha observado flaky en
/// este arnés de Testcontainers — el doble de AccessControl recibe y responde ReserveZoneSlot
/// correctamente y rápido, pero la Saga real del host de pruebas no siempre llega a procesar esa
/// respuesta a tiempo y acaba rechazada por ReservationTimedOut. No reproduce contra el stack real
/// (docker compose, RabbitMQ/Postgres/Mongo persistentes en vez de contenedores efímeros), donde
/// tarjeta y bizum se han verificado a mano repetidamente.
/// </summary>
public sealed class TicketPurchaseFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();

    private WebApplicationFactory<Program> _apiFactory = null!;
    private HttpClient _client = null!;
    private IBusControl _fakeAccessControlBus = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync(), _mongo.StartAsync());

        _apiFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:CheckInDb", _postgres.GetConnectionString());
            builder.UseSetting("RabbitMq:ConnectionString", _rabbitMq.GetConnectionString());
            builder.UseSetting("Mongo:ConnectionString", _mongo.GetConnectionString());
            builder.UseSetting("Mongo:SagaDatabase", "venuepulse_sagas_test");
            // Más generoso que el valor de producción (30s, appsettings.json): el arranque en frío
            // del host + 3 Testcontainers en una máquina bajo carga puede comerse buena parte de
            // ese margen antes de que la primera reserva real llegue a completarse.
            builder.UseSetting("Saga:ReservationTimeoutSeconds", "60");
        });

        _client = _apiFactory.CreateClient();

        // Doble de AccessControl: mismo broker real, responde exactamente lo que respondería
        // el servicio real a cada comando de zona.
        _fakeAccessControlBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(_rabbitMq.GetConnectionString()));
            cfg.ReceiveEndpoint("access-control-fake", e =>
            {
                e.Handler<ReserveZoneSlot>(ctx =>
                    ctx.Publish(new ZoneSlotReserved(ctx.Message.CorrelationId, ctx.Message.ZoneId)));
                e.Handler<ConfirmZoneSlot>(ctx =>
                    ctx.Publish(new ZoneSlotConfirmed(ctx.Message.CorrelationId, ctx.Message.ZoneId)));
                e.Handler<ReleaseZoneSlot>(ctx =>
                    ctx.Publish(new ZoneSlotReleased(ctx.Message.CorrelationId, ctx.Message.ZoneId)));
            });
        });

        await _fakeAccessControlBus.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _fakeAccessControlBus.StopAsync();
        await _apiFactory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Camino_feliz_confirma_la_entrada_de_extremo_a_extremo()
    {
        var ticketId = await PurchaseAsync();
        await PollUntilAsync(ticketId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(90));

        await PayAsync(ticketId, "Card");
        var status = await PollUntilAsync(ticketId, s => s.Status is "Confirmed" or "Rejected", TimeSpan.FromSeconds(90));

        status.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Bizum_compensa_y_rechaza_la_entrada()
    {
        var ticketId = await PurchaseAsync();
        await PollUntilAsync(ticketId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(90));

        await PayAsync(ticketId, "Bizum");
        var status = await PollUntilAsync(ticketId, s => s.Status is "Confirmed" or "Rejected", TimeSpan.FromSeconds(90));

        status.Status.Should().Be("Rejected");
        status.RejectionReason.Should().Be("PaymentFailed");
    }

    private async Task<Guid> PurchaseAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tickets", new
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            ZoneId = Guid.NewGuid(),
            Holder = "Ana",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("ticketId").GetGuid();
    }

    // Entre que el Ticket llega a "AwaitingPayment" (columna de Postgres, lo que consulta
    // PollUntilAsync) y ese mismo hecho queda también confirmado en la Saga (documento de Mongo,
    // lo que valida SubmitPaymentCommandHandler) hay un salto de mensaje real por RabbitMQ — en la
    // UI real esto es invisible porque el botón de pago solo aparece tras la notificación SignalR
    // que dispara ese mismo consumer, pero aquí, sin SignalR, un GET justo tras ver el estado
    // puede llegar a la ventana de unos pocos milisegundos antes de que cierre. Reintentar un par
    // de veces refleja ese mismo comportamiento sin necesitar un cliente SignalR en el test.
    private async Task PayAsync(Guid ticketId, string method)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/pay", new { Method = method });
            if (response.IsSuccessStatusCode)
                return;

            if (attempt == 5)
                response.EnsureSuccessStatusCode();

            await Task.Delay(300);
        }
    }

    private async Task<TicketStatusDto> PollUntilAsync(Guid ticketId, Func<TicketStatusDto, bool> isDone, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (true)
        {
            var response = await _client.GetAsync($"/api/tickets/{ticketId}/status", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<TicketStatusDto>(cancellationToken: cts.Token);
                if (status is not null && isDone(status))
                    return status;
            }

            await Task.Delay(300, cts.Token);
        }
    }
}
