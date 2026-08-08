namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Infrastructure.Consumers;
using VenuePulse.Core.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddAccessControlInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccessControlDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AccessControlDb")));

        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IZoneService, ZoneService>();

        services.AddVenuePulseMessaging(
            configuration["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            x =>
            {
                x.AddConsumer<ReserveZoneSlotConsumer>();
                x.AddConsumer<ConfirmZoneSlotConsumer>();
                x.AddConsumer<ReleaseZoneSlotConsumer>();
                x.AddConsumer<RecordZoneCheckInConsumer>();
                x.AddConsumer<EventCreatedConsumer>();
            });

        return services;
    }
}
