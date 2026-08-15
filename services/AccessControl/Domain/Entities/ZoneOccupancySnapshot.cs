namespace VenuePulse.AccessControl.Domain;

using VenuePulse.Core.Domain;

public sealed class ZoneOccupancySnapshot : Entity<Guid>
{
    public Guid EventId { get; private set; }
    public Guid ZoneId { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public int ConfirmedSold { get; private set; }
    public int CheckedIn { get; private set; }

    private ZoneOccupancySnapshot()
    {
    }

    public ZoneOccupancySnapshot(Guid id, Guid eventId, Guid zoneId, DateTimeOffset recordedAtUtc, int confirmedSold, int checkedIn) : base(id)
    {
        EventId = eventId;
        ZoneId = zoneId;
        RecordedAtUtc = recordedAtUtc;
        ConfirmedSold = confirmedSold;
        CheckedIn = checkedIn;
    }
}
