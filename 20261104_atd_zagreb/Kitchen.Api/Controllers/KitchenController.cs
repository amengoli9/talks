using Kitchen.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using RestaurantDemo;

namespace Kitchen.Api.Controllers;

[ApiController]
[Route("commands")]
public sealed class KitchenController(IKitchenService kitchen) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Accept(OrderRequest order, CancellationToken cancellationToken)
    {
        var result = await kitchen.AcceptAsync(order.Dish, cancellationToken);
        return result.Outcome switch
        {
            KitchenOutcome.Accepted => Ok(new KitchenReply(true, result.Dish!)),
            KitchenOutcome.Unavailable => Conflict(new KitchenReply(false, result.Dish!)),
            KitchenOutcome.Rejected => BadRequest(new { message = "Unknown dish." }),
            KitchenOutcome.ManagementUnavailable => Problem(statusCode: 503, title: "Kitchen availability service unavailable"),
            _ => Problem(statusCode: 503, title: "Kitchen order storage unavailable")
        };
    }
}
