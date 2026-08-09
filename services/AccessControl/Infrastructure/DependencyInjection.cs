namespace VenuePulse.AccessControl.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Application.Simulation;
using VenuePulse.AccessControl.Infrastructure.Consumers;
using VenuePulse.AccessControl.Infrastructure.Inbox;
using VenuePulse.Core.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddAccessControlInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccessControlDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AccessControlDb")));

        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<IOccupancySnapshotRepository, OccupancySnapshotRepository>();

        services.AddSingleton<ISimulationEngine, SimulationEngine>();
        services.AddHostedService<SimulationTickWorker>();

        services.AddScoped(typeof(InboxDeduplicationFilter<>));

        services.AddVenuePulseMessaging(
            configuration["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            x =>
            {
                x.AddConsumer<ReserveZoneSlotConsumer>();
                x.AddConsumer<ConfirmZoneSlotConsumer>();
                x.AddConsumer<ReleaseZoneSlotConsumer>();
                x.AddConsumer<RecordZoneCheckInConsumer>();
                x.AddConsumer<EventCreatedConsumer>();
            },
            (context, cfg) => cfg.UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context));

        return services;
    }
}
