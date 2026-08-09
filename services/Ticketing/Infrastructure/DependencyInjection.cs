namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.Core.Messaging;
using VenuePulse.Ticketing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTicketingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TicketingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("TicketingDb")));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ITicketingService, TicketingService>();

        services.AddVenuePulseMessaging<TicketingDbContext>(
            configuration["RabbitMq:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            x => { });

        return services;
    }
}
