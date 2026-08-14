namespace VenuePulse.CheckIn.Infrastructure;

/// <summary>Configuración del cliente HTTP hacia <c>Ticketing</c> (ver ADR 0028, decisión 2) —
/// única llamada HTTP síncrona entre dos backends del proyecto.</summary>
public sealed record TicketingClientOptions(string BaseUrl, int TimeoutSeconds = 5);
