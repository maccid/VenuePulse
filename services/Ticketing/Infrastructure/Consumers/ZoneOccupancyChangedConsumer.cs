namespace VenuePulse.Ticketing.Infrastructure.Consumers;

using MassTransit;
using Serilog;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Application;

/// <summary>
/// Actualiza la proyección local de aforo (<see cref="IZoneDemandRepository"/>) y, si el evento
/// existe en el catálogo, recalcula y empuja en vivo el precio efectivo de sus tipos de entrada.
/// Sin Inbox: <see cref="ZoneOccupancyChanged"/> es un snapshot que se sobrescribe, procesarlo dos
/// veces no cambia el resultado (ver el propio contrato en Core.Messaging.Contracts).
/// </summary>
public sealed class ZoneOccupancyChangedConsumer(
    IZoneDemandRepository demandRepository,
    ITicketingService ticketingService,
    IPricingNotifier pricingNotifier)
    : IConsumer<ZoneOccupancyChanged>
{
    // Logger estático vía Serilog (no ILogger<T> por constructor): este consumer se resuelve en el
    // harness in-memory de los tests a través de un ServiceCollection sin AddLogging, igual que las
    // sagas — mismo patrón que en TicketPurchaseSaga/PurchaseOrderSaga.
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
