namespace VenuePulse.AccessControl.Domain;

using VenuePulse.Core.Domain;

public sealed class Zone : Entity<Guid>
{
    public Guid EventId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public int Reserved { get; private set; }

    public int ConfirmedSold { get; private set; }

    public int CheckedIn { get; private set; }

    public bool Cancelled { get; private set; }

    public int AvailableToSell => Capacity - Reserved - ConfirmedSold;

    private Zone()
    {
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

    public void ReleaseSlot()
    {
        if (Reserved <= 0)
            throw new InvalidOperationException("No hay ninguna reserva pendiente que liberar.");

        Reserved--;
    }

    public void Cancel()
    {
        if (CheckedIn > 0)
            throw new InvalidOperationException($"La zona '{Name}' no se puede cancelar con gente dentro.");

        Cancelled = true;
    }

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

    public void RecordSimulatedSales(int quantity)
    {
        if (quantity <= 0) return;

        if (AvailableToSell < quantity)
            throw new InvalidOperationException($"La zona '{Name}' está completa.");

        ConfirmedSold += quantity;
    }

    public void RecordSimulatedCheckIns(int quantity)
    {
        if (quantity <= 0) return;

        if (CheckedIn + quantity > ConfirmedSold)
            throw new InvalidOperationException($"No quedan entradas confirmadas sin usar en la zona '{Name}'.");

        CheckedIn += quantity;
    }

    public void ResetCounters()
    {
        Reserved = 0;
        ConfirmedSold = 0;
        CheckedIn = 0;
    }

    public void ResetCheckIns()
    {
        CheckedIn = 0;
    }
}
