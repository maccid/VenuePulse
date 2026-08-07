namespace VenuePulse.CheckIn.Application;

/// <summary>
/// Puerto hacia el exterior en tiempo real (SignalR); la implementación concreta vive en la Api.
/// </summary>
public interface ITicketStatusNotifier
{
    Task NotifyStatusChangedAsync(TicketStatusDto status, CancellationToken cancellationToken);
}
