namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

public sealed record SagaTimeoutOptions(TimeSpan ReservationTimeout, TimeSpan PollInterval);
