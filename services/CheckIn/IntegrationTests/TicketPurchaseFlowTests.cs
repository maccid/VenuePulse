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
            builder.UseSetting("Saga:ReservationTimeoutSeconds", "60");
            builder.UseSetting("Saga:PaymentProcessingDelaySeconds", "0");
        });

        _client = _apiFactory.CreateClient();

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

    [Fact]
    public async Task Cancelar_el_evento_reembolsa_las_confirmadas_y_rechaza_las_pendientes()
    {
        var eventId = Guid.NewGuid();

        var confirmedTicketId = await PurchaseAsync(eventId);
        await PollUntilAsync(confirmedTicketId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(90));
        await PayAsync(confirmedTicketId, "Card");
        await PollUntilAsync(confirmedTicketId, s => s.Status is "Confirmed" or "Rejected", TimeSpan.FromSeconds(90));

        var pendingTicketId = await PurchaseAsync(eventId);
        await PollUntilAsync(pendingTicketId, s => s.Status is "AwaitingPayment" or "Rejected", TimeSpan.FromSeconds(90));

        await _fakeAccessControlBus.Publish(new EventCancelled(eventId));

        var confirmedStatus = await PollUntilAsync(confirmedTicketId, s => s.Status is "Refunded", TimeSpan.FromSeconds(90));
        confirmedStatus.Status.Should().Be("Refunded");

        var pendingStatus = await PollUntilAsync(pendingTicketId, s => s.Status is "Rejected", TimeSpan.FromSeconds(90));
        pendingStatus.RejectionReason.Should().Be("EventCancelled");
    }

    private async Task<Guid> PurchaseAsync(Guid? eventId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/tickets", new
        {
            EventId = eventId ?? Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            ZoneId = Guid.NewGuid(),
            Holder = "Ana",
            Price = 40.00m,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("ticketId").GetGuid();
    }

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
