namespace VenuePulse.AccessControl.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Cancela todas las zonas de un evento al recibir <see cref="EventCancelled"/>.</summary>
public sealed class EventCancelledConsumer(IZoneService zoneService, ILogger<EventCancelledConsumer> logger)
    : IConsumer<EventCancelled>
{
    public async Task Consume(ConsumeContext<EventCancelled> context)
    {
        await zoneService.CancelEventZonesAsync(context.Message.Id, context.CancellationToken);
        logger.LogInformation("Zonas del evento {EventId} canceladas", context.Message.Id);
    }
}

/// <summary>Libera un slot ya confirmado de una zona al recibir <see cref="ReleaseConfirmedZoneSlot"/>.</summary>
public sealed class ReleaseConfirmedZoneSlotConsumer(IZoneService zoneService, ILogger<ReleaseConfirmedZoneSlotConsumer> logger)
    : IConsumer<ReleaseConfirmedZoneSlot>
{
    public async Task Consume(ConsumeContext<ReleaseConfirmedZoneSlot> context)
    {
        var message = context.Message;
        await zoneService.ReleaseConfirmedSlotAsync(message.ZoneId, context.CancellationToken);
        logger.LogInformation("Slot confirmado liberado en zona {ZoneId} para {CorrelationId}", message.ZoneId, message.CorrelationId);
    }
}
