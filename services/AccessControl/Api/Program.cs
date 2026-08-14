using VenuePulse.AccessControl.Api.Hubs;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Infrastructure;
using VenuePulse.Core.Api;
using VenuePulse.Core.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.AccessControl";

builder.Host.UseStructuredLogging(ServiceName);

builder.Services.AddTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317");

builder.Services.AddExceptionHandling();

builder.Services.AddInfrastructure(builder.Configuration);
// Scoped, no Singleton: ahora inyecta IPublishEndpoint (scoped en MassTransit) para publicar
// ZoneOccupancyChanged además del push por SignalR.
builder.Services.AddScoped<IZoneOccupancyNotifier, SignalRZoneOccupancyNotifier>();
builder.Services.AddSignalR();

builder.Services.AddControllersWithStringEnums();
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();
    await AccessControlDbSeeder.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<OccupancyHub>("/hubs/occupancy");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
