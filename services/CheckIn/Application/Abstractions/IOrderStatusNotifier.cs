namespace VenuePulse.CheckIn.Application;

public interface IOrderStatusNotifier
{
    Task NotifyStatusChangedAsync(OrderStatusDto status, CancellationToken cancellationToken);
}
