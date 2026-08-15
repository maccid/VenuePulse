namespace VenuePulse.CheckIn.Application;

public interface ITicketStatusNotifier
{
    Task NotifyStatusChangedAsync(TicketStatusDto status, CancellationToken cancellationToken);
}
