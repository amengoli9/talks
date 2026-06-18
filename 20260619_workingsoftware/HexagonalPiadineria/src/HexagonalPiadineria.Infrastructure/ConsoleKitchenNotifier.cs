using HexagonalPiadineria.Domain;
using HexagonalPiadineria.Domain.Ports;

namespace HexagonalPiadineria.Infrastructure;

public sealed class ConsoleKitchenNotifier : IKitchenNotifier
{
    public Task Notify(Order order, CancellationToken ct = default)
    {
        var piade = order.Lines.Sum(l => l.Quantity);
        Console.WriteLine($"[FORNO] Comanda tavolo {order.Table}: {piade} piade in arrivo.");
        return Task.CompletedTask;
    }
}
