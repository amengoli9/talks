namespace HexagonalPiadineria.Domain.Ports;

public interface IKitchenNotifier
{
    Task Notify(Order order, CancellationToken ct = default);
}
