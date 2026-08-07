namespace VenuePulse.CheckIn.Application;

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.Core.Cqrs;

public static class DependencyInjection
{
    public static IServiceCollection AddCheckInApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
