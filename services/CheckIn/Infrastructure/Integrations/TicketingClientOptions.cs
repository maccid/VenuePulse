namespace VenuePulse.CheckIn.Infrastructure;

public sealed record TicketingClientOptions(string BaseUrl, int TimeoutSeconds = 5);
