using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;

namespace VenuePulse.Core.Observability;

public static class ObservabilityExtensions
{
    public static IHostBuilder UseStructuredLogging(this IHostBuilder builder, string serviceName) =>
        builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({Service}) {Message:lj} traceId={TraceId} {NewLine}{Exception}");

            var seqServerUrl = context.Configuration["Observability:SeqServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqServerUrl))
            {
                configuration.WriteTo.Seq(seqServerUrl);
            }
        });

    public static IServiceCollection AddTracing(
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
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otlpEndpoint);
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        return services;
    }
}
