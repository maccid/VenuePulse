namespace VenuePulse.CheckIn.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using VenuePulse.CheckIn.Api.Contracts;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Application.Commands;
using VenuePulse.CheckIn.Application.Queries;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var lines = request.Lines
            .Select(l => new PlaceOrderLineRequest(l.TicketTypeId, l.ZoneId, l.Quantity, l.UnitPrice))
            .ToList();

        var result = await sender.Send(new PlaceOrderCommand(request.EventId, request.Holder, lines), cancellationToken);

        return result.IsSuccess ? Accepted(new { orderId = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{orderId:guid}/pay")]
    public async Task<IActionResult> Pay(Guid orderId, SubmitOrderPaymentRequest request, CancellationToken cancellationToken)
    {
        var simulateFailure = string.Equals(request.Method, "Bizum", StringComparison.OrdinalIgnoreCase);
        var result = await sender.Send(new SubmitOrderPaymentCommand(orderId, simulateFailure), cancellationToken);
        return result.IsSuccess ? Accepted() : BadRequest(new { error = result.Error });
    }

    [HttpGet("{orderId:guid}/status")]
    public async Task<ActionResult<OrderStatusDto>> GetStatus(Guid orderId, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetOrderStatusQuery(orderId), cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }
}
