using System.Text.Json.Serialization;
using MassTransit;
using VenuePulse.Core.Api;
using VenuePulse.Core.Observability;
using VenuePulse.Ticketing.Api.Hubs;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.Ticketing";

builder.Host.UseStructuredLogging(ServiceName);

builder.Services.AddTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:5341/ingest/otlp/v1/traces");

builder.Services.AddExceptionHandling();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IPricingNotifier, SignalRPricingNotifier>();
builder.Services.AddSignalR();

// Sin esto, EventSummaryDto.Type se serializa como 0/1 (el valor numérico por defecto de
// System.Text.Json para enums) y el frontend, que espera "Match"/"Concert" como en el resto de
// campos de texto de la Api, rompe la fusión zona<->tipo de entrada.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseRequestLogging();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
    await TicketingDbSeeder.SeedAsync(dbContext, publishEndpoint);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<PricingHub>("/hubs/pricing");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
