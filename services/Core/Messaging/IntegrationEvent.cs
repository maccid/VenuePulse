namespace VenuePulse.Core.Messaging;

/// <summary>
/// Contrato de wire entre servicios (no lógica de dominio). Viaja tal cual por RabbitMQ,
/// versionado por namespace si algún día cambia de forma incompatible (ver ADR de contratos).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
