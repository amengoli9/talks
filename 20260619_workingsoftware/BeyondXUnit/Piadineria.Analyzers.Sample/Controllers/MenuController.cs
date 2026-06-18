using Microsoft.AspNetCore.Mvc;
using Piadineria.Analyzers.Sample.Domain;
// using Piadineria.Analyzers.Sample.Infrastructure;   // 🔴 serve solo per la violazione qui sotto

namespace Piadineria.Analyzers.Sample.Controllers;

/// <summary>
/// Cavia per l'analyzer ARCH001 
/// Di default è pulito: dipende solo dalla porta di dominio (IMenuService) → build VERDE.
///
/// 🔴 DEMO §7b: scommenta il campo _db (e la using sopra). Senza eseguire NESSUN test,
/// l'IDE mostra subito la squiggle rossa e `dotnet build` fallisce con:
///     error ARCH001: Il controller 'MenuController' espone un DbContext...
/// La fitness function vive nel COMPILATORE, non in un test runner.
/// </summary>
[ApiController]
[Route("api/menu")]
public sealed class MenuController : ControllerBase
{
    // ✅ Il controller parla col dominio attraverso la porta. Niente database qui.
    private readonly IMenuService _menu;

    // 🔴 Scommenta per innescare ARCH001: un Controller non deve toccare il DbContext.
    // private readonly PiadineriaDbContext _db = null!;

    public MenuController(IMenuService menu) => _menu = menu;

    [HttpGet]
    public async Task<IReadOnlyList<Piada>> GetMenu(CancellationToken ct)
        => await _menu.GetDisponibiliAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Piada>> GetPiada(int id, CancellationToken ct)
        => await _menu.GetByIdAsync(id, ct) is { } piada
            ? Ok(piada)
            : NotFound();
}
