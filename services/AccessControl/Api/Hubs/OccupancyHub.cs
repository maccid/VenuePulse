namespace VenuePulse.AccessControl.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Un único hub, sin grupos: para el tamaño de esta demo todos los clientes conectados
/// (venue-ops, shell) reciben todas las actualizaciones de aforo.
/// </summary>
public sealed class OccupancyHub : Hub;
