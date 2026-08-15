using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

namespace VenuePulse.Core.Observability;

/// <summary>
/// Logging estructurado (Serilog, con TraceId/SpanId de OpenTelemetry como campo de
/// correlación) + telemetría exportada vía OTLP a SigNoz, compartido por los 3 servicios para
/// que un mismo flujo de compra se pueda seguir de punta a punta en SigNoz.
/// Los logs van por el sink Serilog OTLP (mismo endpoint que las trazas): cada log lleva
/// <c>trace_id</c>/<c>span_id</c> del <c>Activity</c> ambiente, así que en SigNoz cada log queda
/// correlacionado con su trace sin código adicional.
/// </summary>
public static class ObservabilityExtensions
{
    public static IHostBuilder UseVenuePulseSerilog(this IHostBuilder builder, string serviceName) =>
        builder.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({Service}) {Message:lj} traceId={TraceId} {NewLine}{Exception}")
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = context.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317";
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                };
            }));

    public static IServiceCollection AddVenuePulseTracing(
        this IServiceCollection services,
        string serviceName,
        string otlpEndpoint)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("MassTransit")
                .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }
}
