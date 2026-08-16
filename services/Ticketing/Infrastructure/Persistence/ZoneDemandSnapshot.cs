namespace VenuePulse.Ticketing.Infrastructure;

public sealed class ZoneDemandSnapshot
{
    public Guid ZoneId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int AvailableToSell { get; set; }
    public int ConfirmedSold { get; set; }
    public int CheckedIn { get; set; }
}
