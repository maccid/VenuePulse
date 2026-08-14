namespace VenuePulse.CheckIn.Application;

using VenuePulse.Core.Domain;

/// <summary>
/// Proyección PROPIA de <c>CheckIn</c> del <c>TicketTypeDto</c> real de <c>Ticketing</c> — solo lo
/// que <c>PlaceOrderCommandHandler</c> necesita para validar el precio de una línea (ver ADR 0028).
/// Deliberadamente no referencia el ensamblado de <c>Ticketing</c> (cada servicio tiene su propia
/// base de datos y sus propios contratos, ver <c>.ia/RULES.md</c>) — la traducción del shape real
/// de la respuesta HTTP vive en <c>TicketingClient</c> (Infrastructure).
/// </summary>
public sealed record TicketTypeSnapshot(Guid Id, decimal EffectivePrice);

/// <summary>
/// Puerto hacia la ÚNICA llamada HTTP síncrona entre dos backends del proyecto (ver ADR 0028,
/// decisión 2) — lectura sin efectos secundarios de un instante puntual, no un flujo continuo. La
/// implementación concreta (HTTP real hacia <c>Ticketing</c>) vive en Infrastructure.
/// </summary>
public interface ITicketingClient
{
    Task<Result<IReadOnlyCollection<TicketTypeSnapshot>>> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken);
}
