using HexagonalPiadineria.Domain.Ports;

namespace HexagonalPiadineria.Domain;

public sealed record OrderLineRequest(string Piada, decimal Price, int Quantity);
public sealed record PlaceOrderRequest(int Table, IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderPlaced(int Table, decimal Total);
public sealed record OrderSummary(int Table, decimal Total);

public sealed class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IKitchenNotifier _kitchen;

    public OrderService(IOrderRepository repository, IKitchenNotifier kitchen)
    {
        _repository = repository;
        _kitchen = kitchen;
    }

    public async Task<OrderPlaced> PlaceOrder(PlaceOrderRequest request, CancellationToken ct = default)
    {
        // 🔴 DEMO performance (§5e) — scommenta questa riga per far sforare la SLO del
        //   load test. Simula una regressione realistica (query lenta / N+1 / lock):
        //   la p95 schizza oltre la soglia e la fitness function di performance diventa
        //   ROSSA, sia con NBomber (.NET) sia con Locust (Python). Ricommenta → verde.
        // await Task.Delay(300, ct);

        if (request.Lines.Count == 0)
            throw new InvalidOperationException("Un ordine deve avere almeno una riga.");
        var lines = request.Lines;
        var order = new Order(request.Table);
        foreach (var line in request.Lines)
            order.Add(new OrderLine(line.Piada, new Euro(line.Price), line.Quantity));

        await _repository.Save(order, ct);
        await _kitchen.Notify(order, ct);

        return new OrderPlaced(order.Table, order.Total.Amount);
    }

    public async Task<IReadOnlyList<OrderSummary>> ListOrders(CancellationToken ct = default)
    {
        var orders = await _repository.GetAll(ct);
        return orders.Select(o => new OrderSummary(o.Table, o.Total.Amount)).ToList();
    }
}
