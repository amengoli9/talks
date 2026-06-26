namespace HexagonalPiadineria.Domain;

public sealed record OrderLine(string Piada, Euro UnitPrice, int Quantity)
{
    public Euro Total => UnitPrice * Quantity;
}

public sealed class Order
{
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines;
    public int Table { get; }

    public Order(int table)
    {
        if (table <= 0) throw new ArgumentOutOfRangeException(nameof(table), "Numero tavolo non valido.");
        Table = table;
    }

    public void Add(OrderLine line)
    {
        if (line.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(line), "Almeno una piada per riga.");
        _lines.Add(line);
    }

    public Euro Subtotal => _lines.Aggregate(new Euro(0), (acc, l) => acc + l.Total);

    public Euro Discount =>
        Subtotal.Amount > 50m ? new Euro(Subtotal.Amount * 0.10m) : new Euro(0);

    public Euro Total => new(Subtotal.Amount - Discount.Amount);
}
