namespace VenuePulse.CheckIn.Application;

public sealed record EventRevenueDto(
    Guid EventId,
    decimal ConfirmedRevenue,
    int ConfirmedCount,
    decimal PendingRevenue,
    int PendingCount);
