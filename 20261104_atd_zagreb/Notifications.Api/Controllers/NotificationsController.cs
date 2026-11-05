using Microsoft.AspNetCore.Mvc;
using Notifications.Api.Domain;
using RestaurantDemo;

namespace Notifications.Api.Controllers;

[ApiController]
[Route("notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Notify(OrderRequest order, CancellationToken cancellationToken)
        => await notifications.NotifyAsync(order.Dish, cancellationToken)
            ? Ok(new NotificationReply(true))
            : BadRequest(new { message = "Unknown dish." });
}
