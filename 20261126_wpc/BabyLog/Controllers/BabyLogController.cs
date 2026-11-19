using BabyLog.Contracts;
using BabyLog.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BabyLog.Controllers;

[ApiController]
[Route("api")]
public sealed class BabyLogController(IBabyLogService service) : ControllerBase
{
    [HttpGet("baby")]
    public BabyProfile Baby() => new("Sofia", new DateOnly(2026, 10, 1));
    [HttpGet("activities")]
    public async Task<ActionResult<ActivityEntry[]>> List([FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (date is null || !BabyLogService.IsSupportedDate(date.Value)) return InvalidDate();
        return (await service.ListAsync(date.Value, ct)).Select(ToEntry).ToArray();
    }
    [HttpPost("activities")]
    public async Task<ActionResult<ActivityEntry>> Create(CreateActivityRequest request, CancellationToken ct)
    {
        try
        {
            var result = ToEntry(await service.CreateAsync(request, ct));
            return Created($"/api/activities/{result.Id}", result);
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: "Attività non valida", detail: ex.Message); }
    }
    [HttpDelete("activities/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
    [HttpGet("summary")]
    public async Task<ActionResult<DailySummary>> Summary([FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (date is null || !BabyLogService.IsSupportedDate(date.Value)) return InvalidDate();
        return await service.SummaryAsync(date.Value, ct);
    }
    private ObjectResult InvalidDate() => Problem(statusCode: 400, title: "Data non valida", detail: "Specificare date tra 0001-01-02 e 9999-12-30 nel formato YYYY-MM-DD.");
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
    private static ActivityEntry ToEntry(Activity a) => new(a.Id, a.Kind, a.StartedAt, a.EndedAt, a.Milliliters, a.Notes);
}
