namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Tipos de entrada por defecto al dar de alta un evento desde Operaciones — reflejan las zonas
/// fijas que AccessControl aprovisiona para cada EventType (ver ZoneService en AccessControl):
/// 3 tipos para partido (Grada Este/Oeste + Grada VIP Norte + Grada VIP Sur), 3 para concierto
/// (Pista + Grada Norte/Sur). El nombre del tipo "especial" de cada tipo (Grada VIP Norte, Grada
/// VIP Sur, Pista) coincide a propósito con el de su zona para que el frontend fusione
/// zona+tipo por coincidencia de nombre (ver ADR 0016 y ADR 0018). Partido pasó de 2 a 3 tipos al
/// revertir el set de 3 zonas del Bloque 15/16 a 4 zonas reales (ver ADR 0024); ADR 0025 invierte
/// qué par de gradas es la VIP cara (Norte/Sur, no Este/Oeste). Precios base fijos, igual que el
/// resto de heurísticas del proyecto — nunca configurables por evento (ver ADR 0019).
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
        EventType.Concert =>
        [
            ("Pista", 60.00m, "Acceso a pista, de pie"),
            ("Grada Norte", 25.00m, "Grada norte"),
            ("Grada Sur", 25.00m, "Grada sur"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
