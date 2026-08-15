namespace VenuePulse.CheckIn.Application;

public interface IReservationTimeoutReader
{
    bool TryGetDeadline(Guid ticketId, out DateTimeOffset deadlineUtc);
}
