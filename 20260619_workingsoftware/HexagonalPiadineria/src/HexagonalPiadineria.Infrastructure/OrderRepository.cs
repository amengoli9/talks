using Microsoft.EntityFrameworkCore;
using HexagonalPiadineria.Domain;
using HexagonalPiadineria.Domain.Ports;

namespace HexagonalPiadineria.Infrastructure;

public sealed class OrderRepository : IOrderRepository
{
    private readonly PiadineriaDbContext _db;

    public OrderRepository(PiadineriaDbContext db) => _db = db;

    public async Task Save(Order order, CancellationToken ct = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Order>> GetAll(CancellationToken ct = default)
        => await _db.Orders.AsNoTracking().Include(o => o.Lines).ToListAsync(ct);
}
