namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record SubmitOrderPaymentCommand(Guid OrderId, bool SimulateFailure) : IRequest<Result>;

/// <summary>
/// Único paso de pago para el pedido completo (ADR 0028) — la <c>PurchaseOrderSaga</c> reenvía
/// <see cref="TicketPaymentSubmitted"/> a cada línea hermana (ver estado <c>AwaitingPayment</c> de
/// esa Saga); este handler solo valida y publica <see cref="PurchaseOrderPaymentSubmitted"/> una
/// vez, mismo patrón que <see cref="SubmitPaymentCommand"/> para una entrada suelta.
/// </summary>
public sealed class SubmitOrderPaymentCommandHandler(IPurchaseOrderRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<SubmitOrderPaymentCommand, Result>
{
    public async Task<Result> Handle(SubmitOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure("El pedido no existe.");

        if (order.Status != PurchaseOrderStatus.AwaitingPayment)
            return Result.Failure($"El pedido no está esperando pago todavía (estado actual: {order.Status}).");

        await publishEndpoint.Publish(new PurchaseOrderPaymentSubmitted(order.Id, request.SimulateFailure), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
