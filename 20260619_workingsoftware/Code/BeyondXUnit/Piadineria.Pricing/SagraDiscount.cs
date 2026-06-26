namespace Piadineria.Pricing;

/// <summary>
/// Sconto "sagra" a scaglioni sull'imponibile. Logica con rami e soglie:
/// terreno perfetto per il MUTATION TESTING (Stryker), che misura quanto
/// i test sono davvero capaci di accorgersi di un bug.
/// </summary>
public static class SagraDiscount
{
    public static decimal Apply(decimal subtotal)
    {
        if (subtotal <= 0m) return 0m;
        if (subtotal > 100m) return subtotal * 0.15m;   // sagra grande
        if (subtotal > 50m) return subtotal * 0.10m;    // sagra piccola
        return 0m;
    }
}
