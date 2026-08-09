using MassTransit;
using VenuePulse.Core.Observability;
using VenuePulse.Ticketing.Api.Hubs;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "VenuePulse.Ticketing";

builder.Host.UseVenuePulseSerilog(ServiceName);

builder.Services.AddVenuePulseTracing(
    ServiceName,
    builder.Configuration["Observability:OtlpEndpoint"] ?? "http://localhost:4317");

builder.Services.AddTicketingInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IPricingNotifier, SignalRPricingNotifier>();
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
