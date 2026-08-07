namespace VenuePulse.AccessControl.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

public sealed class ReserveZoneSlotConsumer(IZoneService zoneService, ILogger<ReserveZoneSlotConsumer> logger)
    : IConsumer<ReserveZoneSlot>
{
    public async Task Consume(ConsumeContext<ReserveZoneSlot> context)
    {
        var message = context.Message;

        var reserved = await zoneService.TryReserveSlotAsync(message.ZoneId, context.CancellationToken);

        if (reserved)
        {
            logger.LogInformation("Slot reservado en zona {ZoneId} para {CorrelationId}", message.ZoneId, message.CorrelationId);
            await context.Publish(new ZoneSlotReserved(message.CorrelationId, message.ZoneId, message.SimulateFailure));
        }
        else
        {
            logger.LogWarning("Zona {ZoneId} sin aforo disponible para {CorrelationId}", message.ZoneId, message.CorrelationId);
            await context.Publish(new ZoneSlotReservationFailed(message.CorrelationId, message.ZoneId, "ZoneFull"));
        }
    }
}

public sealed class ConfirmZoneSlotConsumer(IZoneService zoneService) : IConsumer<ConfirmZoneSlot>
{
    public async Task Consume(ConsumeContext<ConfirmZoneSlot> context)
    {
        var message = context.Message;
        await zoneService.ConfirmSlotAsync(message.ZoneId, context.CancellationToken);
        await context.Publish(new ZoneSlotConfirmed(message.CorrelationId, message.ZoneId));
    }
}

public sealed class ReleaseZoneSlotConsumer(IZoneService zoneService) : IConsumer<ReleaseZoneSlot>
{
    public async Task Consume(ConsumeContext<ReleaseZoneSlot> context)
    {
        var message = context.Message;
        await zoneService.ReleaseSlotAsync(message.ZoneId, context.CancellationToken);
        await context.Publish(new ZoneSlotReleased(message.CorrelationId, message.ZoneId));
    }
}

public sealed class RecordZoneCheckInConsumer(IZoneService zoneService) : IConsumer<RecordZoneCheckIn>
{
    public async Task Consume(ConsumeContext<RecordZoneCheckIn> context)
    {
        var message = context.Message;
        var currentOccupancy = await zoneService.RecordCheckInAsync(message.ZoneId, context.CancellationToken);
        await context.Publish(new ZoneCheckedIn(message.CorrelationId, message.ZoneId, message.TicketId, currentOccupancy));
    }
}
