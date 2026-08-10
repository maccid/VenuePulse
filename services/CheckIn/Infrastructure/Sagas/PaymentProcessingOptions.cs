namespace VenuePulse.CheckIn.Infrastructure.Sagas;

/// <summary>
/// Delay artificial del paso "Procesando pago" (ver ADR 0021) — configurable vía
/// <c>Saga:PaymentProcessingDelaySeconds</c>, bajable a cero en tests para no añadir flakiness
/// (mismo criterio que <see cref="Timeout.SagaTimeoutOptions"/>, ADR 0010).
/// </summary>
public sealed record PaymentProcessingOptions(TimeSpan Delay);
