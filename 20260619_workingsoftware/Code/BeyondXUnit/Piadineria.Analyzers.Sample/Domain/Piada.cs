namespace Piadineria.Analyzers.Sample.Domain;

/// <summary>Modello di dominio: una piada a menù.</summary>
public sealed record Piada(int Id, string Nome, decimal Prezzo, bool Disponibile);
