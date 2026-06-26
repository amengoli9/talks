using Piadineria.Pricing;

namespace Piadineria.Pricing.Tests;

/// <summary>
/// Questi test sono VERDI, ma uno usa un oracolo DEBOLE: controlla che ci sia
/// "uno sconto", non QUANTO. Stryker lo smaschera: muta la percentuale e la
/// soglia e i mutanti SOPRAVVIVONO. Morale: "verde" non vuol dire "coperto".
/// </summary>
public class SagraDiscountTests
{
    [Fact]
    public void Small_orders_pay_full_price()
        => Assert.Equal(0m, SagraDiscount.Apply(10m));

    // Oracolo debole: "c'è uno sconto" lascia vivi i mutanti su 0.10 / 0.15 e sulle soglie.
    [Fact]
    public void Big_orders_get_some_discount()
        => Assert.True(SagraDiscount.Apply(120m) > 0m);
}
