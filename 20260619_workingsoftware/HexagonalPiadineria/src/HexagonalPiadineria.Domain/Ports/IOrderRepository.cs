namespace HexagonalPiadineria.Domain.Ports;

public interface IOrderRepository
{
    Task Save(Order order, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetAll(CancellationToken ct = default);
}
