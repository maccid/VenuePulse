namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Cascada de cancelación de evento (ADR 0021). Nunca muta el <see cref="Ticket"/> directamente —
/// solo decide, por estado, qué mensaje corresponde a cada entrada y lo publica: una entrada
/// <see cref="TicketStatus.Confirmed"/> se reembolsa (<see cref="TicketRefunded"/> +
/// <see cref="ReleaseConfirmedZoneSlot"/>); una entrada todavía <see cref="TicketStatus.Pending"/>/
/// <see cref="TicketStatus.AwaitingPayment"/> se compensa a través de la propia
/// <c>TicketPurchaseSaga</c> (<see cref="TicketPurchaseCancelled"/>). La mutación real la hacen
/// los consumers de resultado correspondientes (<see cref="TicketRefundedConsumer"/>, la Saga) —
/// mismo reparto de responsabilidades que ya usan <see cref="TicketPurchaseConfirmedConsumer"/>/
/// <see cref="TicketPurchaseRejectedConsumer"/>.
/// Naturalmente idempotente sin Inbox nuevo: tras la primera pasada ya no quedan entradas en esos
/// estados para este evento, así que una redelivery de <see cref="EventCancelled"/> no encuentra
/// nada que reenviar. Una entrada en <see cref="TicketStatus.ProcessingPayment"/> queda fuera a
/// propósito — el pago ya está en curso resolviéndose solo, ver el <c>Ignore(Cancelled)</c> en
/// <c>TicketPurchaseSaga.Confirming</c> y el riesgo aceptado documentado en el ADR 0021.
/// </summary>
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

/// <summary>Mismo patrón que los consumers de resultado en <c>TicketPurchaseOutcomeConsumers.cs</c>
/// — muta el Ticket y notifica, en vez de dejar que lo haga <see cref="EventCancelledConsumer"/>.</summary>
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
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), null),
            context.CancellationToken);
    }
}
