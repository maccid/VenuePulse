namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// El aforo ya está reservado de verdad en AccessControl y la Saga ya transicionó a
/// "AwaitingPayment" — refleja ese hecho en el propio Ticket para que la Api pueda rechazar un
/// /pay prematuro con un error limpio en vez de dejar que el evento de pago llegue a la Saga en
/// un estado que no lo espera. Escucha <see cref="TicketAwaitingPayment"/> (publicado por la
/// propia Saga tras su transición), NO el <see cref="ZoneSlotReserved"/> original: reaccionar al
/// mismo evento en paralelo que la Saga, sin esperar a que ella termine, deja una ventana real en
/// la que el Ticket en Postgres dice "AwaitingPayment" antes de que la Saga en Mongo lo esté de
/// verdad — visto en real, no es un caso hipotético (ver ADR 0018).
/// </summary>
public sealed class TicketReadyForPaymentConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<TicketReadyForPaymentConsumer> logger) : IConsumer<TicketAwaitingPayment>
{
    public async Task Consume(ConsumeContext<TicketAwaitingPayment> context)
    {
        var ticket = await repository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("TicketAwaitingPayment para una entrada inexistente {TicketId}", context.Message.CorrelationId);
            return;
        }

        ticket.MarkAwaitingPayment();
        await repository.SaveChangesAsync(context.CancellationToken);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), null, expiresAtUtc),
            context.CancellationToken);
    }
}

/// <summary>
/// Publicado por la propia Saga justo antes de esperar el delay configurable de "Procesando
/// pago" (ver ADR 0021) — refleja ese paso transitorio en el Ticket para que el timeline por
/// SignalR lo muestre como un paso real, mismo patrón que <see cref="TicketReadyForPaymentConsumer"/>.
/// </summary>
public sealed class TicketProcessingPaymentConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<TicketProcessingPaymentConsumer> logger) : IConsumer<TicketProcessingPayment>
{
    public async Task Consume(ConsumeContext<TicketProcessingPayment> context)
    {
        var ticket = await repository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("TicketProcessingPayment para una entrada inexistente {TicketId}", context.Message.CorrelationId);
            return;
        }

        ticket.MarkProcessingPayment();
        await repository.SaveChangesAsync(context.CancellationToken);

        // El watchdog ya des-trackeó al recibir TicketPaymentSubmitted (el cliente ya eligió
        // método de pago) — aquí siempre será null, es el mismo lookup que en el resto de
        // consumidores para no bifurcar el criterio (ver ADR 0025).
        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), null, expiresAtUtc),
            context.CancellationToken);
    }
}

public sealed class TicketPurchaseConfirmedConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<TicketPurchaseConfirmedConsumer> logger) : IConsumer<TicketPurchaseConfirmed>
{
    public async Task Consume(ConsumeContext<TicketPurchaseConfirmed> context)
    {
        var ticket = await repository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("TicketPurchaseConfirmed para una entrada inexistente {TicketId}", context.Message.CorrelationId);
            return;
        }

        ticket.Confirm();
        await repository.SaveChangesAsync(context.CancellationToken);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), null, expiresAtUtc),
            context.CancellationToken);
    }
}

public sealed class TicketPurchaseRejectedConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<TicketPurchaseRejectedConsumer> logger) : IConsumer<TicketPurchaseRejected>
{
    public async Task Consume(ConsumeContext<TicketPurchaseRejected> context)
    {
        var ticket = await repository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (ticket is null)
        {
            logger.LogWarning("TicketPurchaseRejected para una entrada inexistente {TicketId}", context.Message.CorrelationId);
            return;
        }

        ticket.Reject(context.Message.Reason);
        await repository.SaveChangesAsync(context.CancellationToken);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.RejectionReason, expiresAtUtc),
            context.CancellationToken);
    }
}
