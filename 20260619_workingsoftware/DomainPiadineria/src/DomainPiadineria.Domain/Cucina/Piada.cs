namespace DomainPiadineria.Domain.Cucina;

public sealed class Piada
{
    public Farcitura Farcitura { get; }
    public decimal Prezzo { get; }

    public Piada(Farcitura farcitura, decimal prezzo)
    {
        if (prezzo <= 0) throw new ArgumentOutOfRangeException(nameof(prezzo), "Una piada ha un prezzo.");
        Farcitura = farcitura;
        Prezzo = prezzo;
    }

    public bool Vegetariana =>
        Farcitura is Farcitura.SquacqueroneERucola or Farcitura.NutellaPerIBurdel;
}
