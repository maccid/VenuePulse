namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.Core.Messaging.Contracts;

public sealed class TicketPurchaseConfirmedConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
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

        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.Status.ToString(), null),
            context.CancellationToken);
    }
}

public sealed class TicketPurchaseRejectedConsumer(
    ITicketRepository repository,
    ITicketStatusNotifier notifier,
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

        await notifier.NotifyStatusChangedAsync(
            new TicketStatusDto(ticket.Id, ticket.Status.ToString(), ticket.RejectionReason),
            context.CancellationToken);
    }
}
