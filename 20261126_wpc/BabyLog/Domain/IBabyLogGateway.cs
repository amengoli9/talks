using BabyLog.Contracts;

namespace BabyLog.Domain;

public interface IBabyLogGateway
{
    Task<BabyProfile> GetBabyAsync(CancellationToken cancellationToken = default);
    Task<ActivityEntry[]> GetActivitiesAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<DailySummary> GetSummaryAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<ActivityEntry> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
