namespace DomainPiadineria.Domain.Cucina;

public enum StatoComanda { InCoda, InCottura, Pronta }

public sealed class Comanda
{
    private readonly List<Piada> _piade;
    public int Tavolo { get; }
    public IReadOnlyList<Piada> Piade => _piade;
    public StatoComanda Stato { get; private set; } = StatoComanda.InCoda;

    public Comanda(int tavolo, IEnumerable<Piada> piade)
    {
        if (tavolo <= 0) throw new ArgumentOutOfRangeException(nameof(tavolo), "Numero tavolo non valido.");
        _piade = piade.ToList();
        if (_piade.Count == 0) throw new ArgumentException("Una comanda ha almeno una piada.", nameof(piade));
    }

    public void Inizia()
    {
        if (Stato != StatoComanda.InCoda)
            throw new InvalidOperationException("La comanda è già in lavorazione.");
        Stato = StatoComanda.InCottura;
    }

    public void SegnaPronta()
    {
        if (Stato != StatoComanda.InCottura)
            throw new InvalidOperationException("Si segna pronta solo una comanda in cottura.");
        Stato = StatoComanda.Pronta;
    }
}
