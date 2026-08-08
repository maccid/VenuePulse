namespace VenuePulse.CheckIn.Infrastructure.Sagas;

public sealed record SagaTimeoutOptions(TimeSpan ReservationTimeout, TimeSpan PollInterval);
