namespace VenuePulse.CheckIn.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Consumers;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Data;
using VenuePulse.Core.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CheckInDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CheckInDb"))
                .AddInterceptors(new AuditingSaveChangesInterceptor()));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();

        services.AddSingleton(new TicketingClientOptions(
            configuration["Ticketing:BaseUrl"] ?? "http://localhost:5101",
            int.Parse(configuration["Ticketing:TimeoutSeconds"] ?? "5")));
        services.AddHttpClient<ITicketingClient, TicketingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<TicketingClientOptions>();
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton(new SagaTimeoutOptions(
            ReservationTimeout: TimeSpan.FromSeconds(int.Parse(configuration["Saga:ReservationTimeoutSeconds"] ?? "60")),
            PollInterval: TimeSpan.FromSeconds(int.Parse(configuration["Saga:TimeoutPollIntervalSeconds"] ?? "5"))));
        services.AddSingleton<ReservationTimeoutTracker>();
        // Mismo singleton expuesto también por su puerto de Application (contador de reserva,
        // ver ADR 0025) — GetTicketStatusQueryHandler no depende de Infrastructure directamente.
        services.AddSingleton<IReservationTimeoutReader>(sp => sp.GetRequiredService<ReservationTimeoutTracker>());
        services.AddHostedService<ReservationTimeoutMonitor>();

        services.AddSingleton(new PaymentProcessingOptions(
            Delay: TimeSpan.FromSeconds(int.Parse(configuration["Saga:PaymentProcessingDelaySeconds"] ?? "3"))));

        services.AddMessaging<CheckInDbContext>(
            configuration["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            x =>
            {
                x.AddMongoSaga<TicketPurchaseSaga, TicketPurchaseSagaState>(
                    configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017",
                    configuration["Mongo:SagaDatabase"] ?? "venuepulse_sagas");
                x.AddMongoSaga<PurchaseOrderSaga, PurchaseOrderSagaState, PurchaseOrderSagaDefinition>(
                    configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017",
                    configuration["Mongo:SagaDatabase"] ?? "venuepulse_sagas");

                x.AddConsumer<TicketReadyForPaymentConsumer>();
                x.AddConsumer<TicketProcessingPaymentConsumer>();
                x.AddConsumer<TicketPurchaseConfirmedConsumer>();
                x.AddConsumer<TicketPurchaseRejectedConsumer>();
                x.AddConsumer<PurchaseOrderReadyForPaymentConsumer>();
                x.AddConsumer<PurchaseOrderCompletedConsumer>();
                x.AddConsumer<PurchaseOrderRejectedConsumer>();
                x.AddConsumer<ReservationWatchdogConsumer>();
                // Nombre de cola explícito: mismo motivo que en AccessControl.Infrastructure.
                // DependencyInjection — sin esto, ambos EventCancelledConsumer comparten cola.
                x.AddConsumer<EventCancelledConsumer>()
                    .Endpoint(e => e.Name = "CheckIn.EventCancelled");
                x.AddConsumer<TicketRefundedConsumer>();
            });

        return services;
    }
}
