using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Api.Hubs;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure;
using VenuePulse.Core.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.CheckIn";

builder.Host.UseVenuePulseSerilog(ServiceName);

builder.Services.AddVenuePulseTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317");

builder.Services.AddCheckInApplication();
builder.Services.AddCheckInInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ITicketStatusNotifier, SignalRTicketStatusNotifier>();
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
    var dbContext = scope.ServiceProvider.GetRequiredService<CheckInDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<TicketStatusHub>("/hubs/tickets");
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
