namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;

public sealed class TicketPurchaseSagaDefinition : SagaDefinition<TicketPurchaseSagaState>
{
    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<TicketPurchaseSagaState> sagaConfigurator,
        IRegistrationContext context)
    {
        sagaConfigurator.UsePartitioner(8, (SagaConsumeContext<TicketPurchaseSagaState> sagaContext) => sagaContext.Saga.CorrelationId);
        endpointConfigurator.UseMessageRetry(r => r.Interval(15, TimeSpan.FromMilliseconds(200)));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
