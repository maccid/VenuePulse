namespace VenuePulse.AccessControl.Infrastructure.Inbox;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VenuePulse.Core.Messaging;

public sealed class InboxDeduplicationFilter<TMessage>(
    AccessControlDbContext dbContext,
    ILogger<InboxDeduplicationFilter<TMessage>> logger)
    : IFilter<ConsumeContext<TMessage>>
    where TMessage : IntegrationEvent
{
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        var eventId = context.Message.EventId;

        var alreadyProcessed = await dbContext.ProcessedMessages
            .AnyAsync(p => p.EventId == eventId, context.CancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Mensaje {MessageType} {EventId} ya procesado, se descarta por redelivery",
                typeof(TMessage).Name, eventId);
            return;
        }

        await dbContext.ProcessedMessages.AddAsync(
            new ProcessedMessage(eventId, typeof(TMessage).Name, DateTimeOffset.UtcNow),
            context.CancellationToken);

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("inboxDeduplication");
}
