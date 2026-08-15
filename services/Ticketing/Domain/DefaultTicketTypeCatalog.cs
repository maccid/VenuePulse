namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

public static class DefaultTicketTypeCatalog
{
    public static IReadOnlyList<(string Name, decimal Price, string? Description)> For(EventType type) => type switch
    {
        EventType.Match =>
        [
            ("General", 40.00m, "Grada Este u Oeste, sin asiento numerado"),
            ("Grada VIP Norte", 90.00m, "Grada Norte, acceso a hospitality"),
            ("Grada VIP Sur", 90.00m, "Grada Sur, acceso a hospitality"),
        ],
        EventType.Concert =>
        [
            ("Grada 1", 25.00m, "Grada 1"),
            ("Grada 2", 25.00m, "Grada 2"),
            ("Grada 3", 25.00m, "Grada 3"),
            ("Pista", 60.00m, "Acceso a pista, de pie"),
            ("VIP 1", 90.00m, "VIP junto al escenario"),
            ("VIP 2", 90.00m, "VIP junto al escenario"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
