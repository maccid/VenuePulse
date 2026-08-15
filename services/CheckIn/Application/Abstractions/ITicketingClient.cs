namespace VenuePulse.CheckIn.Application;

using VenuePulse.Core.Domain;

public sealed record TicketTypeSnapshot(Guid Id, decimal EffectivePrice);

public interface ITicketingClient
{
    Task<Result<IReadOnlyCollection<TicketTypeSnapshot>>> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken);
}
