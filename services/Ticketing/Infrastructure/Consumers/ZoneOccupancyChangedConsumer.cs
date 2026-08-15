namespace VenuePulse.Ticketing.Infrastructure.Consumers;

using MassTransit;
using Serilog;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Application;

/// <summary>Consume ZoneOccupancyChanged: actualiza la proyección local de aforo y recalcula el
/// precio efectivo de los tipos de entrada del evento.</summary>
public sealed class ZoneOccupancyChangedConsumer(
    IZoneDemandRepository demandRepository,
    ITicketingService ticketingService,
    IPricingNotifier pricingNotifier)
    : IConsumer<ZoneOccupancyChanged>
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ZoneOccupancyChangedConsumer>();

    public async Task Consume(ConsumeContext<ZoneOccupancyChanged> context)
    {
        var message = context.Message;

        await demandRepository.UpsertAsync(
            message.ZoneId,
            message.Id,
            message.Capacity,
            message.AvailableToSell,
            message.ConfirmedSold,
            message.CheckedIn,
            context.CancellationToken);

        Logger.Information("Proyección de aforo actualizada para la zona {ZoneId} (aforo {Capacity}, {ConfirmedSold} confirmadas, {CheckedIn} en recinto)", message.ZoneId, message.Capacity, message.ConfirmedSold, message.CheckedIn);

        var ticketTypes = await ticketingService.GetTicketTypesAsync(message.Id, context.CancellationToken);
        if (ticketTypes is not null)
        {
            await pricingNotifier.NotifyPricingChangedAsync(message.Id, ticketTypes, context.CancellationToken);
            Logger.Information("Precios recalculados para el evento {EventId} tras la actualización de la zona {ZoneId}", message.Id, message.ZoneId);
        }
    }
}
