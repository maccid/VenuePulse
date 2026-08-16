namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;

public sealed class PurchaseOrderSagaDefinition : SagaDefinition<PurchaseOrderSagaState>
{
    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<PurchaseOrderSagaState> sagaConfigurator,
        IRegistrationContext context)
    {
        sagaConfigurator.UsePartitioner(8, (SagaConsumeContext<PurchaseOrderSagaState> sagaContext) => sagaContext.Saga.CorrelationId);
        endpointConfigurator.UseMessageRetry(r => r.Interval(15, TimeSpan.FromMilliseconds(200)));
        endpointConfigurator.UseInMemoryOutbox(context);
    }
}
