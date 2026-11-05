using Microsoft.AspNetCore.Mvc;
using RestaurantDemo;

namespace Orders.Api.Controllers;

[ApiController]
[Route("menu")]
public sealed class MenuController : ControllerBase
{
    [HttpGet]
    public ActionResult<MenuItem[]> Get() => Menu.Items;
}
