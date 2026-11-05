using Microsoft.AspNetCore.Mvc;
using Orders.Api.Domain;
using RestaurantDemo;

namespace Orders.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Confirm(OrderRequest order, CancellationToken cancellationToken)
    {
        var result = await orders.ConfirmAsync(order.Dish, cancellationToken);
        return result.Outcome switch
        {
            OrderOutcome.Accepted => Ok(new
            {
                message = result.Message, dish = result.Dish,
                notificationSent = result.NotificationSent, traceId = result.TraceId
            }),
            OrderOutcome.Rejected => BadRequest(new { message = result.Message, traceId = result.TraceId }),
            OrderOutcome.Unavailable => Conflict(new { message = result.Message, traceId = result.TraceId }),
            _ => StatusCode(result.Outcome == OrderOutcome.TimedOut ? 504 : 502,
                new { message = result.Message, kitchenAccepted = result.KitchenAccepted, traceId = result.TraceId })
        };
    }
}
