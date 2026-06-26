using Microsoft.EntityFrameworkCore;
using Piadineria.Analyzers.Sample.Domain;

namespace Piadineria.Analyzers.Sample.Infrastructure;

/// <summary>
/// Implementazione della porta IMenuService. QUI il DbContext è lecito:
/// l'analyzer non scatta perché la classe non finisce per "Controller".
/// È esattamente il punto: il database sta dietro al dominio, non davanti.
/// </summary>
public sealed class MenuService : IMenuService
{
    private readonly PiadineriaDbContext _db;

    public MenuService(PiadineriaDbContext db) => _db = db;

    public async Task<IReadOnlyList<Piada>> GetDisponibiliAsync(CancellationToken ct = default)
        => await _db.Piade.Where(p => p.Disponibile).ToListAsync(ct);

    public Task<Piada?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Piade.FirstOrDefaultAsync(p => p.Id == id, ct);
}
