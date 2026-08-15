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

public sealed class PurchaseOrderFlowTests(Xunit.Abstractions.ITestOutputHelper output) : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();
    private readonly FakeTicketingClient _fakeTicketingClient = new();

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
                services.RemoveAll<ITicketingClient>();
                services.AddSingleton<ITicketingClient>(_fakeTicketingClient);
            });
        });

        _client = _apiFactory.CreateClient();
        _zoneWithoutAvailability = Guid.NewGuid();

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
    public async Task Cancelar_un_pedido_esperando_pago_lo_rechaza_y_libera_el_aforo_reservado()
    {
        var ticketTypeId = Guid.NewGuid();
        _fakeTicketingClient.SetPrice(ticketTypeId, 40.00m);

        var orderId = await PlaceOrderAsync(
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m),
            (ticketTypeId, Guid.NewGuid(), 1, 40.00m));

        await PollUntilAsync(orderId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(180));
        await CancelOrderAsync(orderId);
        var status = await PollUntilAsync(orderId, s => s.Status is "Rejected", TimeSpan.FromSeconds(180));

        status.RejectionReason.Should().Be("CancelledByCustomer");
        status.Tickets.Should().HaveCount(2);
        status.Tickets.Should().OnlyContain(t => t.Status == "Rejected");
    }

    [Fact]
    public async Task No_se_puede_cancelar_un_pedido_ya_completado()
    {
        var ticketTypeId = Guid.NewGuid();
        _fakeTicketingClient.SetPrice(ticketTypeId, 40.00m);

        var orderId = await PlaceOrderAsync((ticketTypeId, Guid.NewGuid(), 1, 40.00m));

        await PollUntilAsync(orderId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(180));
        await PayOrderAsync(orderId, "Card");
        await PollUntilAsync(orderId, s => s.Status is "Completed" or "Rejected", TimeSpan.FromSeconds(180));

        var response = await _client.PostAsync($"/api/orders/{orderId}/cancel", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Precio_desactualizado_rechaza_el_pedido_sin_crear_ninguna_entrada()
    {
        var ticketTypeId = Guid.NewGuid();
        var holder = $"precio-cambiado-{Guid.NewGuid():N}";
        _fakeTicketingClient.SetPrice(ticketTypeId, 45.00m);

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            EventId = Guid.NewGuid(),
            Holder = holder,
            Lines = new[] { new { TicketTypeId = ticketTypeId, ZoneId = Guid.NewGuid(), Quantity = 1, UnitPrice = 40.00m } },
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.Should().Be("PriceChanged");

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

    private async Task CancelOrderAsync(Guid orderId)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await _client.PostAsync($"/api/orders/{orderId}/cancel", null);
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
