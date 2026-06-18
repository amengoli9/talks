using Microsoft.AspNetCore.Mvc;
using HexagonalPiadineria.Domain;

// 🔴 DEMO erosione (§1) — 1/2: scommenta anche questo using.
// using Microsoft.EntityFrameworkCore;

namespace HexagonalPiadineria.WebApp.Controllers;

[ApiController]
[Route("orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService) => _orderService = orderService;

    [HttpPost]
    [ProducesResponseType(typeof(OrderPlaced), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderPlaced>> Create(PlaceOrderRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _orderService.PlaceOrder(request, ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummary>>> List(CancellationToken ct)
        => Ok(await _orderService.ListOrders(ct));

    // ─────────────────────────────────────────────────────────────────────────────
    // 🔴 DEMO erosione (§1) — 2/2: il GET /orders dà solo la lista; "mi serviva il
    //   dettaglio di un tavolo e l'ho preso al volo dal DbContext", invece di esporlo
    //   dallo use case. Scommenta l'using EF in testa e il metodo qui sotto: la build
    //   resta VERDE, l'endpoint compare in Scalar e funziona... ma adesso il controller
    //   interroga DIRETTAMENTE il database (DbContext via [FromServices]), scavalcando
    //   porta e use case. → Controller_should_not_depend_on_persistence diventa ROSSO.
    // ─────────────────────────────────────────────────────────────────────────────
    // [HttpGet("{table:int}")]
    // [ProducesResponseType(StatusCodes.Status200OK)]
    // [ProducesResponseType(StatusCodes.Status404NotFound)]
    // public async Task<IActionResult> GetByTable(
    //     int table,
    //     [FromServices] HexagonalPiadineria.Infrastructure.PiadineriaDbContext db,
    //     CancellationToken ct)
    // {
    //     var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Table == table, ct);
    //     return order is null ? NotFound() : Ok(new { order.Table, total = order.Total.Amount });
    // }
}
