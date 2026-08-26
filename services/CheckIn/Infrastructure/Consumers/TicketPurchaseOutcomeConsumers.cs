namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Consume TicketAwaitingPayment: marca la entrada como esperando pago y notifica el nuevo estado.</summary>
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
        logger.LogInformation("Entrada {TicketId} lista para elegir método de pago", ticket.Id);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.Price, null, expiresAtUtc),
            context.CancellationToken);
    }
}

/// <summary>Consume TicketProcessingPayment: marca la entrada como procesando pago y notifica el nuevo estado.</summary>
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
        logger.LogInformation("Entrada {TicketId} procesando pago", ticket.Id);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.Price, null, expiresAtUtc),
            context.CancellationToken);
    }
}

/// <summary>Consume TicketPurchaseConfirmed: marca la entrada como confirmada y notifica el nuevo estado.</summary>
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
        logger.LogInformation("Entrada {TicketId} confirmada", ticket.Id);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.Price, null, expiresAtUtc),
            context.CancellationToken);
    }
}

/// <summary>Consume TicketPurchaseRejected: marca la entrada como rechazada y notifica el nuevo estado.</summary>
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
        logger.LogWarning("Entrada {TicketId} rechazada: {Reason}", ticket.Id, ticket.RejectionReason);

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.Price, ticket.RejectionReason, expiresAtUtc),
            context.CancellationToken);
    }
}
