namespace VenuePulse.CheckIn.IntegrationTests;

using System.Collections.Concurrent;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using VenuePulse.CheckIn.Application;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Prueba la <c>PurchaseOrderSaga</c> (ADR 0028) de extremo a extremo contra infraestructura REAL
/// (Postgres, RabbitMQ, MongoDB vía Testcontainers) — mismo criterio que
/// <see cref="TicketPurchaseFlowTests"/>, del que reutiliza el patrón de doble de
/// <c>AccessControl</c>. <c>Ticketing</c> tampoco se levanta como servicio: <see cref="FakeTicketingClient"/>
/// sustituye la única llamada HTTP síncrona del proyecto (ver ADR 0028, decisión 2) sin necesitar un
/// tercer host real.
///
/// NOTA (ver .ia/KNOWN-ISSUES.md, "PurchaseOrderFlowTests: ida sin vuelta del doble de
/// AccessControl"): en esta máquina se ha observado que <c>ReserveZoneSlot</c> SÍ llega al doble
/// de <c>AccessControl</c> de este fichero (confirmado con logging directo) y este SÍ publica
/// <c>ZoneSlotReserved</c>/<c>ZoneSlotReservationFailed</c> de vuelta, pero esa respuesta no
/// siempre llega a procesarse por la <c>TicketPurchaseSaga</c> real — el watchdog de timeout
/// (ADR 0010) acaba rechazando la reserva a los 60s exactos, comportamiento correcto del watchdog
/// ante un mensaje que nunca llega, no un fallo de la lógica de negocio (verificada aparte,
/// unitariamente e inspeccionando Postgres/Mongo en directo). Mismo tipo de intermitencia de
/// Testcontainers ya aceptada para <see cref="TicketPurchaseFlowTests"/> — si aparece, no es un
/// bug de la <c>PurchaseOrderSaga</c> en sí.
/// </summary>
public sealed class PurchaseOrderFlowTests(Xunit.Abstractions.ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();
    private readonly FakeTicketingClient _fakeTicketingClient = new();

    // ZoneId reservado en cada test para forzar el camino "sin aforo" del doble de AccessControl —
    // cualquier otro ZoneId reserva con éxito.
    private Guid _zoneWithoutAvailability;

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
            builder.UseSetting("Mongo:SagaDatabase", "venuepulse_sagas_test_orders");
            builder.UseSetting("Saga:ReservationTimeoutSeconds", "60");
            builder.UseSetting("Saga:PaymentProcessingDelaySeconds", "0");

            builder.ConfigureTestServices(services =>
            {
                // Sustituye la ÚNICA llamada HTTP síncrona del proyecto (ADR 0028) — sin levantar
                // Ticketing real, el fake decide directamente qué EffectivePrice está vigente.
                services.RemoveAll<ITicketingClient>();
                services.AddSingleton<ITicketingClient>(_fakeTicketingClient);
            });
        });

        _client = _apiFactory.CreateClient();
        _zoneWithoutAvailability = Guid.NewGuid();

        // Doble de AccessControl: mismo broker real. Toda ReserveZoneSlot reserva con éxito salvo
        // la zona marcada como sin aforo — así una misma tanda de peticiones concurrentes de un
        // pedido puede tener una línea que falla y otras que no, sin depender de agotar de verdad
        // un contador real.
        _fakeAccessControlBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(new Uri(_rabbitMq.GetConnectionString()));
            cfg.ReceiveEndpoint("access-control-fake-orders", e =>
            {
                e.Handler<ReserveZoneSlot>(ctx => ctx.Message.ZoneId == _zoneWithoutAvailability
                    ? ctx.Publish(new ZoneSlotReservationFailed(ctx.Message.CorrelationId, ctx.Message.ZoneId, "ZoneFull"))
                    : ctx.Publish(new ZoneSlotReserved(ctx.Message.CorrelationId, ctx.Message.ZoneId)));
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
    public async Task Pedido_de_dos_lineas_confirma_las_dos_de_extremo_a_extremo()
    {
        var ticketTypeId = Guid.NewGuid();
        _fakeTicketingClient.SetPrice(ticketTypeId, 40.00m);

        var orderId = await PlaceOrderAsync(
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m),
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m));

        await PollUntilAsync(orderId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(180));
        await PayOrderAsync(orderId, "Card");
        var status = await PollUntilAsync(orderId, s => s.Status is "Completed" or "Rejected", TimeSpan.FromSeconds(180));

        status.Status.Should().Be("Completed");
        status.Tickets.Should().HaveCount(2);
        status.Tickets.Should().OnlyContain(t => t.Status == "Confirmed");
    }

    [Fact]
    public async Task Una_linea_sin_aforo_rechaza_el_pedido_completo_y_libera_la_otra()
    {
        var ticketTypeId = Guid.NewGuid();
        _fakeTicketingClient.SetPrice(ticketTypeId, 40.00m);

        var orderId = await PlaceOrderAsync(
            (ticketTypeId, _zoneWithoutAvailability, 1, 40.00m),
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m));

        var status = await PollUntilAsync(orderId, s => s.Status is "Rejected", TimeSpan.FromSeconds(180));

        status.RejectionReason.Should().Be("InsufficientAvailability");
        status.Tickets.Should().HaveCount(2);
        // Ninguna de las dos entradas queda "colgada" reservando aforo: la que falló se rechaza
        // directa, la hermana que sí había reservado se cancela y libera (ver ADR 0028).
        status.Tickets.Should().OnlyContain(t => t.Status == "Rejected");
    }

    [Fact]
    public async Task Bizum_en_todo_el_pedido_compensa_y_rechaza_las_dos_lineas()
    {
        var ticketTypeId = Guid.NewGuid();
        _fakeTicketingClient.SetPrice(ticketTypeId, 40.00m);

        var orderId = await PlaceOrderAsync(
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m),
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m));

        await PollUntilAsync(orderId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(180));
        await PayOrderAsync(orderId, "Bizum");
        var status = await PollUntilAsync(orderId, s => s.Status is "Completed" or "Rejected", TimeSpan.FromSeconds(180));

        status.Status.Should().Be("Rejected");
        status.RejectionReason.Should().Be("PaymentFailed");
        status.Tickets.Should().OnlyContain(t => t.Status == "Rejected");
    }

    [Fact]
    public async Task Precio_desactualizado_rechaza_el_pedido_sin_crear_ninguna_entrada()
    {
        var ticketTypeId = Guid.NewGuid();
        var holder = $"precio-cambiado-{Guid.NewGuid():N}";
        _fakeTicketingClient.SetPrice(ticketTypeId, 45.00m); // precio vigente real

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            EventId = Guid.NewGuid(),
            Holder = holder,
            Lines = new[] { new { TicketTypeId = ticketTypeId, ZoneId = Guid.NewGuid(), Quantity = 1, UnitPrice = 40.00m } }, // desactualizado
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.Should().Be("PriceChanged");

        // Un pedido rechazado por precio no debe dejar ningún rastro (ver PlaceOrderCommandHandler).
        var myTickets = await _client.GetFromJsonAsync<List<TicketSummaryDto>>($"/api/tickets?holder={holder}");
        myTickets.Should().BeEmpty();
    }

    private sealed record ErrorResponse(string Error);

    private async Task<Guid> PlaceOrderAsync(params (Guid TicketTypeId, Guid ZoneId, int Quantity, decimal UnitPrice)[] lines)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            EventId = Guid.NewGuid(),
            Holder = "Ana",
            Lines = lines.Select(l => new { l.TicketTypeId, l.ZoneId, l.Quantity, l.UnitPrice }),
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("orderId").GetGuid();
    }

    // Mismo motivo que PayAsync en TicketPurchaseFlowTests: pequeña ventana entre que el pedido
    // aparece en AwaitingPayment (Postgres) y la PurchaseOrderSaga (Mongo) ya está de verdad lista
    // para aceptar el pago.
    private async Task PayOrderAsync(Guid orderId, string method)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync($"/api/orders/{orderId}/pay", new { Method = method });
            if (response.IsSuccessStatusCode)
                return;

            if (attempt == 5)
                response.EnsureSuccessStatusCode();

            await Task.Delay(300);
        }
    }

    private async Task<OrderStatusDto> PollUntilAsync(Guid orderId, Func<OrderStatusDto, bool> isDone, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        string? lastLogged = null;

        while (true)
        {
            var response = await _client.GetAsync($"/api/orders/{orderId}/status", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<OrderStatusDto>(cancellationToken: cts.Token);
                if (status is not null)
                {
                    var snapshot = $"order={status.Status} reason={status.RejectionReason} tickets=[{string.Join(",", status.Tickets.Select(t => $"{t.TicketId:N}:{t.Status}:{t.RejectionReason}"))}]";
                    if (snapshot != lastLogged)
                    {
                        output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {snapshot}");
                        lastLogged = snapshot;
                    }

                    if (isDone(status))
                        return status;
                }
            }
            else
            {
                output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] GET status -> {(int)response.StatusCode}");
            }

            await Task.Delay(300, cts.Token);
        }
    }

    /// <summary>Doble en memoria de <see cref="ITicketingClient"/> — el test fija directamente qué
    /// <c>EffectivePrice</c> está "vigente" para cada <c>TicketTypeId</c>, sin levantar Ticketing
    /// real (mismo criterio que el doble de AccessControl: infraestructura real donde importa —
    /// Postgres/RabbitMQ/Mongo — doble solo en el borde externo que no es objeto de esta prueba).</summary>
    private sealed class FakeTicketingClient : ITicketingClient
    {
        private readonly ConcurrentDictionary<Guid, decimal> _prices = new();

        public void SetPrice(Guid ticketTypeId, decimal effectivePrice) => _prices[ticketTypeId] = effectivePrice;

        public Task<Result<IReadOnlyCollection<TicketTypeSnapshot>>> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<TicketTypeSnapshot> snapshots = _prices
                .Select(kv => new TicketTypeSnapshot(kv.Key, kv.Value))
                .ToList();
            return Task.FromResult(Result.Success(snapshots));
        }
    }
}
