namespace VenuePulse.CheckIn.Domain;

public enum TicketStatus
{
    Pending,
    AwaitingPayment,

    /// <summary>
    /// Transitorio, entre <see cref="AwaitingPayment"/> y <see cref="Confirmed"/>/<see cref="Rejected"/>:
    /// la Saga ya sabe qué método de pago eligió el cliente y está resolviendo el resultado tras
    /// el delay configurable de "Procesando pago" (ver ADR 0021).
    /// </summary>
    ProcessingPayment,

    Confirmed,
    Rejected,
    CheckedIn,

    /// <summary>
    /// Estado terminal distinto de <see cref="Rejected"/>: una entrada reembolsada sí llegó a
    /// completarse (estuvo <see cref="Confirmed"/>) antes de que su evento se cancelara — ver ADR
    /// 0021.
    /// </summary>
    Refunded
}
