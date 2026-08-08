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

    public int AvailableToSell => Capacity - Reserved - ConfirmedSold;

    private Zone()
    {
        // EF Core
    }

    public Zone(Guid id, Guid eventId, string name, int capacity) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La zona necesita un nombre.", nameof(name));
        if (capacity <= 0)
            throw new ArgumentException("El aforo debe ser positivo.", nameof(capacity));

        EventId = eventId;
        Name = name;
        Capacity = capacity;
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
