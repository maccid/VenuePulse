namespace VenuePulse.CheckIn.Domain;

public enum TicketStatus
{
    Pending,
    AwaitingPayment,
    ProcessingPayment,
    Confirmed,
    Rejected,
    CheckedIn,
    Refunded
}
