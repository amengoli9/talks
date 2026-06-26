namespace HexagonalPiadineria.Domain;

public readonly record struct Euro(decimal Amount)
{
    public static Euro operator +(Euro a, Euro b) => new(a.Amount + b.Amount);
    public static Euro operator *(Euro a, int times) => new(a.Amount * times);

    public override string ToString() => $"€ {Amount:0.00}";
}
