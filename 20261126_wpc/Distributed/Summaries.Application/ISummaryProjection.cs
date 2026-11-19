using BabyLog.Distributed.Summaries.Domain;

namespace BabyLog.Distributed.Summaries.Application;

public interface ISummaryProjection
{
    Task ApplyRecordedAsync(SummaryActivity activity, CancellationToken ct);
    Task ApplyDeletedAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SummaryActivity>> FindAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
}
