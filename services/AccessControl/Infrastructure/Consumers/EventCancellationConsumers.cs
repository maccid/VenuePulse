namespace VenuePulse.AccessControl.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

// Cancelación de evento (ADR 0020). Ambos quedan cubiertos gratis por el Inbox genérico
// (InboxDeduplicationFilter<> registrado en el bus, ADR 0015): la redelivery de EventCancelled o
// de un ReleaseConfirmedZoneSlot no re-aplica el efecto.
public sealed class EventCancelledConsumer(IZoneService zoneService, ILogger<EventCancelledConsumer> logger)
    : IConsumer<EventCancelled>
{
    public async Task Consume(ConsumeContext<EventCancelled> context)
    {
        await zoneService.CancelEventZonesAsync(context.Message.Id, context.CancellationToken);
        logger.LogInformation("Zonas del evento {EventId} canceladas", context.Message.Id);
    }
}

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
