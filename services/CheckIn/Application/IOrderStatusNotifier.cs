namespace VenuePulse.CheckIn.Application;

/// <summary>
/// Puerto hacia el exterior en tiempo real (SignalR) para el estado agregado de un pedido — mismo
/// patrón que <see cref="ITicketStatusNotifier"/> (ver ADR 0028); la implementación concreta vive
/// en la Api, reutilizando el hub <c>/hubs/tickets</c> ya existente con un evento nuevo
/// (<c>orderStatusChanged</c>), sin hub propio.
/// </summary>
public interface IOrderStatusNotifier
{
    Task NotifyStatusChangedAsync(OrderStatusDto status, CancellationToken cancellationToken);
}
