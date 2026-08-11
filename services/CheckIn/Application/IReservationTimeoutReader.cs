namespace VenuePulse.CheckIn.Application;

/// <summary>
/// Puerto de solo lectura hacia el deadline de reserva en curso de un ticket — la implementación
/// concreta vive en Infrastructure (<c>ReservationTimeoutTracker</c>, ver ADR 0010/0025). Permite
/// que la Application (p. ej. <c>GetTicketStatusQueryHandler</c>) exponga la cuenta atrás sin
/// depender de la capa de Infrastructure.
/// </summary>
public interface IReservationTimeoutReader
{
    bool TryGetDeadline(Guid ticketId, out DateTimeOffset deadlineUtc);
}
