namespace VenuePulse.CheckIn.Infrastructure;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.Core.Domain;

public sealed class TicketingClient(HttpClient httpClient, ILogger<TicketingClient> logger) : ITicketingClient
{
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
