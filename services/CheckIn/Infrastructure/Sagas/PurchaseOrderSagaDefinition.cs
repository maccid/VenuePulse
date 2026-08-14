namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;

/// <summary>
/// Serializa por pedido (<c>PurchaseOrderSagaState.CorrelationId</c>) el procesamiento de mensajes
/// en el endpoint de <see cref="PurchaseOrderSaga"/>. Sin esto, un pedido de N líneas puede recibir
/// hasta N eventos (<c>TicketAwaitingPayment</c>/<c>TicketPurchaseConfirmed</c>/
/// <c>TicketPurchaseRejected</c>) casi simultáneos publicados por sus <c>TicketPurchaseSaga</c>
/// hermanas — MassTransit los reparte a varios hilos en paralelo, que chocan al escribir a la vez
/// el mismo documento de esta Saga en MongoDB (concurrencia optimista). <c>UseMessageRetry</c> (ver
/// <see cref="Core.Messaging.MessagingExtensions"/>) mitiga colisiones puntuales con 3 reintentos
/// cortos, pero cuantas más líneas tenga el pedido, más probable es agotarlos: el mensaje que
/// pierde la carrera acaba en la cola de error, esa línea nunca sale de
/// <see cref="PurchaseOrderSagaState.PendingTicketIds"/> y el pedido se queda para siempre en
/// <c>ReservingLines</c> sin llegar a <c>AwaitingPayment</c> — sin fallo visible, solo silencio.
///
/// <c>UsePartitioner</c> sobre <see cref="ISagaConfigurator{TSaga}"/> (no sobre el endpoint
/// genérico) parte por <c>SagaConsumeContext.Saga.CorrelationId</c> — MassTransit ya ha resuelto
/// la instancia correlacionada en ese punto del pipe, así que la misma clave sirve sin importar
/// cuál de los 5 tipos de mensaje que consume esta Saga haya llegado. Elimina la carrera de raíz:
/// mensajes del mismo pedido nunca se procesan a la vez, pero pedidos distintos de otros
/// compradores siguen en paralelo sin penalización (particiones asignadas por hash del
/// <c>CorrelationId</c>, ver <c>partitionCount</c>).
/// </summary>
public sealed class PurchaseOrderSagaDefinition : SagaDefinition<PurchaseOrderSagaState>
{
    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<PurchaseOrderSagaState> sagaConfigurator,
        IRegistrationContext context)
    {
        sagaConfigurator.UsePartitioner(8, (SagaConsumeContext<PurchaseOrderSagaState> sagaContext) => sagaContext.Saga.CorrelationId);
    }
}
