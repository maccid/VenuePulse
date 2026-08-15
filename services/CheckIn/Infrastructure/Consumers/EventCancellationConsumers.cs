namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Consume EventCancelled: por cada entrada del evento, publica el mensaje de compensación que corresponda según su estado.</summary>
public sealed class EventCancelledConsumer(
    ITicketRepository repository,
    IPublishEndpoint publishEndpoint,
    ILogger<EventCancelledConsumer> logger) : IConsumer<EventCancelled>
{
    public async Task Consume(ConsumeContext<EventCancelled> context)
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
                case TicketStatus.Pending or TicketStatus.AwaitingPayment:
                    await publishEndpoint.Publish(new TicketPurchaseCancelled(ticket.Id), context.CancellationToken);
                    break;
            }
        }

        await repository.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Cascada de cancelación publicada para el evento {EventId} ({Count} entradas revisadas)",
            context.Message.Id, tickets.Count);
    }
}

/// <summary>Consume TicketRefunded: marca la entrada como reembolsada y notifica el nuevo estado.</summary>
public sealed class TicketRefundedConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
    ILogger<TicketRefundedConsumer> logger) : IConsumer<TicketRefunded>
{
    public async Task Consume(ConsumeContext<TicketRefunded> context)
    {
        var ticket = await repository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("TicketRefunded para una entrada inexistente {TicketId}", context.Message.CorrelationId);
            return;
        }

        ticket.Refund();
        await repository.SaveChangesAsync(context.CancellationToken);

        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), null, null),
            context.CancellationToken);
    }
}
