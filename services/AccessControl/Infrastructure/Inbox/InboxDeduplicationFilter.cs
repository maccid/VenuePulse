namespace VenuePulse.AccessControl.Infrastructure.Inbox;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VenuePulse.Core.Messaging;

/// <summary>
/// Inbox real de <c>AccessControl</c> (ver ADR 0015 y Bloque 3 en <c>.ia/TODO.md</c>): sin esto,
/// una redelivery de RabbitMQ (entrega "al menos una vez") duplicaba reservas/check-ins reales —
/// solo <c>EventCreatedConsumer</c> tenía idempotencia propia (<c>ExistsForEventAsync</c>), el
/// resto de comandos de zona no.
/// Se registra el marcador ANTES de delegar al consumer, sin llamar a <c>SaveChangesAsync</c>
/// aquí: el propio consumer comparte el mismo <see cref="AccessControlDbContext"/> con ámbito por
/// mensaje (scope de DI de MassTransit), así que el <c>SaveChangesAsync</c> que ya hace
/// <c>ZoneRepository.SaveAsync</c> persiste el marcador y el cambio de negocio en la misma
/// transacción — atómico sin tocar cada consumer a mano. Si el consumer lanza antes de guardar,
/// el marcador queda solo en el change tracker (nunca se persiste) y el mensaje se puede
/// reprocesar sin problema.
/// </summary>
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
