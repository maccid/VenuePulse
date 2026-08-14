namespace VenuePulse.CheckIn.Application;

// Confirmed = Confirmed+CheckedIn (venta ya cerrada); Pending = AwaitingPayment+ProcessingPayment
// (todavía puede acabar en venta o en rechazo). Refunded/Rejected quedan fuera por construcción —
// ver ADR 0021: un reembolso baja los ingresos solo por dejar de sumar, sin necesidad de restarlo.
public sealed record EventRevenueDto(
    Guid EventId,
    decimal ConfirmedRevenue,
    int ConfirmedCount,
    decimal PendingRevenue,
    int PendingCount);
