namespace VenuePulse.CheckIn.Infrastructure;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.Core.Domain;

/// <summary>
/// Única llamada HTTP síncrona entre dos backends del proyecto (ver ADR 0028, decisión 2):
/// <c>GET /api/events/{eventId}/ticket-types</c>, endpoint ya existente de <c>Ticketing</c> (ver
/// <c>VenuePulse.Ticketing.Api.Controllers.EventsController</c>). Sin Polly (a propósito, ver ADR
/// 0028) — cualquier fallo (red, timeout, deserialización) se traduce a un único
/// <see cref="Result"/> de fallo, sin reintentos. La causa del fallo queda loggeada aquí (con el
/// <c>EventId</c> afectado) para que se pueda distinguir en Seq un problema de red de un timeout
/// o de un contrato roto — el command solo ve <c>PricingUnavailable</c>.
/// </summary>
public sealed class TicketingClient(HttpClient httpClient, ILogger<TicketingClient> logger) : ITicketingClient
{
    // Shape MÍNIMO del TicketTypeDto real de Ticketing (Id/EffectivePrice) que este cliente
    // necesita — deserialización tolerante a los campos extra (Name/Price/BasePrice/...) de
    // System.Text.Json, sin referenciar el ensamblado de Ticketing (ver .ia/RULES.md, "un
    // microservicio, sus propios contratos").
    private sealed record TicketTypeResponse(Guid Id, decimal EffectivePrice);

    public async Task<Result<IReadOnlyCollection<TicketTypeSnapshot>>> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/events/{eventId}/ticket-types", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ticketing devolvió {StatusCode} al consultar precios del evento {EventId}", response.StatusCode, eventId);
                return Result.Failure<IReadOnlyCollection<TicketTypeSnapshot>>("PricingUnavailable");
            }

            var ticketTypes = await response.Content.ReadFromJsonAsync<List<TicketTypeResponse>>(cancellationToken: cancellationToken);
            if (ticketTypes is null)
            {
                logger.LogWarning("Ticketing devolvió un cuerpo vacío al consultar precios del evento {EventId}", eventId);
                return Result.Failure<IReadOnlyCollection<TicketTypeSnapshot>>("PricingUnavailable");
            }

            IReadOnlyCollection<TicketTypeSnapshot> snapshots = ticketTypes
                .Select(t => new TicketTypeSnapshot(t.Id, t.EffectivePrice))
                .ToList();

            return Result.Success(snapshots);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Error de red al consultar precios del evento {EventId}", eventId);
            return Result.Failure<IReadOnlyCollection<TicketTypeSnapshot>>("PricingUnavailable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout del propio HttpClient (HttpClient.Timeout), distinto de una cancelación real
            // del caller — mismo motivo de fallo que un error de red.
            logger.LogWarning("Timeout al consultar precios del evento {EventId}", eventId);
            return Result.Failure<IReadOnlyCollection<TicketTypeSnapshot>>("PricingUnavailable");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Respuesta de Ticketing no deserializable para el evento {EventId} — ¿contrato roto?", eventId);
            return Result.Failure<IReadOnlyCollection<TicketTypeSnapshot>>("PricingUnavailable");
        }
    }
}
