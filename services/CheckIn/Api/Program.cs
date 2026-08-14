using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Api.Hubs;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure;
using VenuePulse.Core.Api;
using VenuePulse.Core.Observability;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.CheckIn";

builder.Host.UseStructuredLogging(ServiceName);

builder.Services.AddTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317");

builder.Services.AddExceptionHandling();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ITicketStatusNotifier, SignalRTicketStatusNotifier>();
builder.Services.AddSingleton<IOrderStatusNotifier, SignalROrderStatusNotifier>();
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

app.UseExceptionHandler();

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
