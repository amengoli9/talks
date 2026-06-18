// 🔴 DEMO isolamento domini — 1/2: scommenta anche questo using.
// using DomainPiadineria.Domain.Cucina;

namespace DomainPiadineria.Domain.Sala;

public sealed record OrderLine(string Piada, decimal UnitPrice, int Quantity)
{
    public int Quantity { get; } =
        Quantity > 0 ? Quantity : throw new ArgumentOutOfRangeException(nameof(Quantity), "Almeno una piada per riga.");

    public decimal Total => UnitPrice * Quantity;
}

public sealed class Order
{
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines;
    public int Table { get; }

    public Order(int table) => Table = table;

    public void Add(OrderLine line) => _lines.Add(line);

    // 🔴 DEMO isolamento domini — 2/2: "per comodità" la Sala costruisce la riga
    //   direttamente dalla Piada della Cucina, invece di riceverne uno snapshot
    //   (nome + prezzo). Così la Sala dipende dalla Cucina e i due domini non sono
    //   più isolati: Sala_should_not_depend_on_Cucina diventa ROSSO.
    // public void AddFromKitchen(Piada piada, int quantity) =>
    //     Add(new OrderLine(piada.Farcitura.ToString(), piada.Prezzo, quantity));

    public decimal Subtotal => _lines.Sum(l => l.Total);

    public decimal Discount => Subtotal > 50m ? Subtotal * 0.10m : 0m;

    public decimal Total => Subtotal - Discount;
}
