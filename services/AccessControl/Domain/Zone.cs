namespace VenuePulse.AccessControl.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Una puerta/zona del recinto. CRUD con un par de invariantes claros — no hace falta CQRS
/// aquí, ver ADR 0003.
/// </summary>
public sealed class Zone : Entity<Guid>
{
    /// <summary>Event al que pertenece esta zona — ver .ia/KNOWN-ISSUES.md sobre el aforo
    /// compartido entre eventos que este campo resuelve.</summary>
    public Guid EventId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Aforo máximo de venta (no de ocupación física instantánea).</summary>
    public int Capacity { get; private set; }

    /// <summary>Reservas en curso, parte de una Saga de compra sin confirmar todavía.</summary>
    public int Reserved { get; private set; }

    /// <summary>Entradas confirmadas/vendidas para esta zona.</summary>
    public int ConfirmedSold { get; private set; }

    /// <summary>Personas físicamente dentro ahora mismo — lo que alimenta el dashboard en vivo.</summary>
    public int CheckedIn { get; private set; }

    /// <summary>Zona cerrada por cancelación del evento (ADR 0019/0020): el evento solo se cancela
    /// mientras está OnSale, así que nunca habrá gente dentro (CheckedIn == 0).</summary>
    public bool Cancelled { get; private set; }

    public int AvailableToSell => Capacity - Reserved - ConfirmedSold;

    private Zone()
    {
        // EF Core
    }

    public Zone(Guid id, Guid eventId, string name, int capacity) : base(id)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name), "La zona necesita un nombre.");
        Capacity = Guard.AgainstNonPositive(capacity, nameof(capacity), "El aforo debe ser positivo.");

        EventId = eventId;
    }

    public void ReserveSlot()
    {
        if (AvailableToSell <= 0)
            throw new InvalidOperationException($"La zona '{Name}' está completa.");

        Reserved++;
    }

    public void ConfirmSlot()
    {
        if (Reserved <= 0)
            throw new InvalidOperationException("No hay ninguna reserva pendiente que confirmar.");

        Reserved--;
        ConfirmedSold++;
    }

    /// <summary>Compensación de la Saga: libera una reserva que no llegó a confirmarse.</summary>
    public void ReleaseSlot()
    {
        if (Reserved <= 0)
            throw new InvalidOperationException("No hay ninguna reserva pendiente que liberar.");

        Reserved--;
    }

    /// <summary>Cancela la zona (evento cancelado, ADR 0020). Idempotente a propósito: la redelivery
    /// de <c>EventCancelled</c> (Inbox genérico aparte) o una llamada repetida no deben fallar.
    /// No toca los contadores — el aforo confirmado lo libera CheckIn ticket a ticket vía
    /// <see cref="ReleaseConfirmedSlot"/>.</summary>
    public void Cancel()
    {
        if (CheckedIn > 0)
            throw new InvalidOperationException($"La zona '{Name}' no se puede cancelar con gente dentro.");

        Cancelled = true;
    }

    /// <summary>Compensación por cancelación del evento: libera una entrada YA confirmada
    /// (decrementa <see cref="ConfirmedSold"/>), no una reserva en curso — eso es
    /// <see cref="ReleaseSlot"/>. Antes de este comando, <c>ConfirmedSold</c> solo subía; ver
    /// ADR 0020.</summary>
    public void ReleaseConfirmedSlot()
    {
        if (ConfirmedSold <= 0)
            throw new InvalidOperationException("No hay entradas confirmadas que liberar.");

        ConfirmedSold--;
    }

    public void RecordCheckIn()
    {
        if (CheckedIn >= ConfirmedSold)
            throw new InvalidOperationException($"No quedan entradas confirmadas sin usar en la zona '{Name}'.");

        CheckedIn++;
    }

    /// <summary>Venta simulada (motor de demo): confirma directamente sin pasar por reserva ni
    /// Saga, respetando el aforo vendible. Existe para que el demo pueda generar demanda.</summary>
    public void RecordSimulatedSales(int quantity)
    {
        if (quantity <= 0) return;

        if (AvailableToSell < quantity)
            throw new InvalidOperationException($"La zona '{Name}' está completa.");

        ConfirmedSold += quantity;
    }

    /// <summary>Check-in simulado (motor de demo): personas que pasan el torniquete sin entrada
    /// real. Nunca supera <see cref="ConfirmedSold"/>, así que el no-show queda representado
    /// de forma natural (CheckedIn &lt; ConfirmedSold).</summary>
    public void RecordSimulatedCheckIns(int quantity)
    {
        if (quantity <= 0) return;

        if (CheckedIn + quantity > ConfirmedSold)
            throw new InvalidOperationException($"No quedan entradas confirmadas sin usar en la zona '{Name}'.");

        CheckedIn += quantity;
    }

    /// <summary>Pone el aforo a cero — reinicia la simulación del evento para poder repetir el
    /// demo. Solo contadores, no elimina la zona.</summary>
    public void ResetCounters()
    {
        Reserved = 0;
        ConfirmedSold = 0;
        CheckedIn = 0;
    }
}
