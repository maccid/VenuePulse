namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Tipos de entrada por defecto al dar de alta un evento desde Operaciones — reflejan las zonas
/// fijas que AccessControl aprovisiona para cada EventType (ver ZoneService en AccessControl):
/// 3 tipos para partido (Grada Este/Oeste + Grada VIP Norte + Grada VIP Sur), 6 para concierto
/// (Grada 1/2/3 + Pista + VIP 1/2, ver ADR 0026). El nombre del tipo "especial" de cada uno
/// coincide a propósito con el de su zona para que el frontend fusione zona+tipo por coincidencia
/// de nombre (ver ADR 0016 y ADR 0018). Partido pasó de 2 a 3 tipos al revertir el set de 3 zonas
/// del Bloque 15/16 a 4 zonas reales (ver ADR 0024); ADR 0025 invierte qué par de gradas es la VIP
/// cara (Norte/Sur, no Este/Oeste). Precios base fijos, igual que el resto de heurísticas del
/// proyecto — nunca configurables por evento (ver ADR 0019).
/// </summary>
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
        // Grada < Pista < VIP (ver ADR 0026): VIP es el precio más alto de los tres por estar
        // junto al escenario, al mismo precio que la Grada VIP de partido (ADR 0024) por
        // continuidad entre tipos de evento, sin relación real en el backend.
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
