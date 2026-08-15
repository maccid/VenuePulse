using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace VenuePulse.Core.Api;

public static class ApiExtensions
{
    public static IMvcBuilder AddControllersWithStringEnums(this IServiceCollection services) =>
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services) =>
        services
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddProblemDetails();

    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
