namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Tipos de entrada por defecto al dar de alta un evento desde Operaciones — reflejan las zonas
/// fijas que AccessControl aprovisiona para cada EventType (ver ZoneService en AccessControl):
/// 2 tipos para partido (Puerta Norte/Sur + Grada VIP Este/Oeste, 4 zonas), 4 tipos para
/// concierto (Pista VIP + Pista + Grada Frontal + Grada Lateral Este/Oeste, 5 zonas). Precios
/// base fijos, igual que el resto de heurísticas del proyecto — nunca configurables por evento
/// (ver ADR 0019).
/// </summary>
public static class DefaultTicketTypeCatalog
{
    public static IReadOnlyList<(string Name, decimal Price, string? Description)> For(EventType type) => type switch
    {
        EventType.Match =>
        [
            ("General", 40.00m, "Puerta Norte o Sur, sin asiento numerado"),
            ("VIP", 90.00m, "Grada VIP, acceso a hospitality"),
        ],
        EventType.Concert =>
        [
            ("Pista VIP", 95.00m, "Delante del escenario, acceso preferente"),
            ("Pista", 60.00m, "Acceso a pista, de pie"),
            ("Grada Frontal", 40.00m, "Grada frente al escenario"),
            ("Grada Lateral", 25.00m, "Grada lateral"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
