namespace VenuePulse.AccessControl.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

public sealed class EventCreatedConsumer(IZoneService zoneService, ILogger<EventCreatedConsumer> logger)
    : IConsumer<EventCreated>
{
    public async Task Consume(ConsumeContext<EventCreated> context)
    {
        var message = context.Message;

        await zoneService.ProvisionZonesForEventAsync(message.Id, message.Type, context.CancellationToken);

        logger.LogInformation("Zonas aprovisionadas para el evento {EventId} ({Name})", message.Id, message.Name);
    }
}
