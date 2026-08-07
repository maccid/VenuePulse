using VenuePulse.AccessControl.Api.Hubs;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Infrastructure;
using VenuePulse.Core.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.AccessControl";

builder.Host.UseVenuePulseSerilog(ServiceName);

builder.Services.AddVenuePulseTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317");

builder.Services.AddAccessControlInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IZoneOccupancyNotifier, SignalRZoneOccupancyNotifier>();
builder.Services.AddSignalR();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

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
