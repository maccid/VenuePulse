namespace VenuePulse.Core.Domain;

/// <summary>
/// Distingue el tipo de recinto/aforo que necesita un Event — determina qué zonas aprovisiona
/// AccessControl al recibir EventCreated (ver ZoneService en AccessControl.Application).
/// </summary>
public enum EventType
{
    Match,
    Concert,
}
