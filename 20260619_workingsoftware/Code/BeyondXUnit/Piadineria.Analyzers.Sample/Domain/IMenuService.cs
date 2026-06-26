namespace Piadineria.Analyzers.Sample.Domain;

/// <summary>
/// La "porta" (in senso esagonale) che il Controller può vedere.
/// Il dominio espone capacità di business, non tabelle né query.
/// </summary>
public interface IMenuService
{
    Task<IReadOnlyList<Piada>> GetDisponibiliAsync(CancellationToken ct = default);

    Task<Piada?> GetByIdAsync(int id, CancellationToken ct = default);
}
