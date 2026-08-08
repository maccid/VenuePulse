namespace VenuePulse.CheckIn.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Consumers;
using VenuePulse.CheckIn.Infrastructure.Sagas;
using VenuePulse.Core.Data;
using VenuePulse.Core.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddCheckInInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CheckInDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CheckInDb"))
                .AddInterceptors(new AuditingSaveChangesInterceptor()));

        services.AddScoped<ITicketRepository, TicketRepository>();

        services.AddSingleton(new SagaTimeoutOptions(
            ReservationTimeout: TimeSpan.FromSeconds(int.Parse(configuration["Saga:ReservationTimeoutSeconds"] ?? "30")),
            PollInterval: TimeSpan.FromSeconds(int.Parse(configuration["Saga:TimeoutPollIntervalSeconds"] ?? "5"))));
        services.AddSingleton<ReservationTimeoutTracker>();
        services.AddHostedService<ReservationTimeoutMonitor>();

        services.AddVenuePulseMessaging<CheckInDbContext>(
            configuration["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            x =>
            {
                x.AddVenuePulseMongoSaga<TicketPurchaseSaga, TicketPurchaseSagaState>(
                    configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017",
                    configuration["Mongo:SagaDatabase"] ?? "venuepulse_sagas");

                x.AddConsumer<TicketPurchaseConfirmedConsumer>();
                x.AddConsumer<TicketPurchaseRejectedConsumer>();
                x.AddConsumer<ReservationWatchdogConsumer>();
            });

        return services;
    }
}
