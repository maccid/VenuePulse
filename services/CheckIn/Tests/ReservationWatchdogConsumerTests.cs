namespace VenuePulse.CheckIn.Tests;

using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Messaging.Contracts;

public class ReservationWatchdogConsumerTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness, ReservationTimeoutTracker Tracker)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddSingleton(new SagaTimeoutOptions(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5)))
            .AddSingleton<ReservationTimeoutTracker>()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<ReservationWatchdogConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness, provider.GetRequiredService<ReservationTimeoutTracker>());
    }

    [Fact]
    public async Task Cancelar_un_ticket_deja_de_vigilarlo()
    {
        var (provider, harness, tracker) = await StartHarnessAsync();
        await using var _ = provider;

        var ticketId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        await harness.Bus.Publish(new TicketPurchaseRequested(ticketId, zoneId));
        await harness.Consumed.Any<TicketPurchaseRequested>();
        tracker.TryGetDeadline(ticketId, out var deadlineBeforeCancel).Should().BeTrue();

        await harness.Bus.Publish(new TicketPurchaseCancelled(ticketId, "CancelledByCustomer"));
        await harness.Consumed.Any<TicketPurchaseCancelled>();
        tracker.TryGetDeadline(ticketId, out var deadlineAfterCancel).Should().BeFalse();
    }
}
