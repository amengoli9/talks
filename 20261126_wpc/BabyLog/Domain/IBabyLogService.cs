using BabyLog.Contracts;

namespace BabyLog.Domain;

public interface IBabyLogService
{
    Task<IReadOnlyList<Activity>> ListAsync(DateOnly date, CancellationToken ct = default);
    Task<Activity> CreateAsync(CreateActivityRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<DailySummary> SummaryAsync(DateOnly date, CancellationToken ct = default);
}
