namespace VenuePulse.Ticketing.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Un único hub, sin grupos: mismo criterio que OccupancyHub en AccessControl, el tamaño de esta
/// demo no justifica scoping por evento en el propio hub (se filtra en el cliente).
/// </summary>
public sealed class PricingHub : Hub;
