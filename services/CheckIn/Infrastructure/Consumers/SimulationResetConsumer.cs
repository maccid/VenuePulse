namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Consume SimulationReset: al reiniciar la simulación, cancela y reembolsa todas las entradas del evento para que CheckIn quede limpio.</summary>
public sealed class SimulationResetConsumer(
    ITicketRepository repository,
    IPublishEndpoint publishEndpoint,
    ILogger<SimulationResetConsumer> logger) : IConsumer<SimulationReset>
{
    public async Task Consume(ConsumeContext<SimulationReset> context)
    {
        var tickets = await repository.GetByEventIdAsync(context.Message.Id, context.CancellationToken);

        foreach (var ticket in tickets)
        {
            switch (ticket.Status)
            {
                case TicketStatus.Confirmed:
                    await publishEndpoint.Publish(new TicketRefunded(ticket.Id), context.CancellationToken);
                    await publishEndpoint.Publish(new ReleaseConfirmedZoneSlot(ticket.Id, ticket.ZoneId), context.CancellationToken);
                    break;
                case TicketStatus.Pending or TicketStatus.AwaitingPayment or TicketStatus.ProcessingPayment:
                    await publishEndpoint.Publish(new TicketPurchaseCancelled(ticket.Id), context.CancellationToken);
                    break;
            }
        }

        await repository.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Reset de simulación: canceladas {Count} entradas para el evento {EventId}",
            tickets.Count, context.Message.Id);
    }
}
