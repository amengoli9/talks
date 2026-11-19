using BabyLog.Distributed.Events;
using BabyLog.Distributed.Summaries.Domain;

namespace BabyLog.Distributed.Summaries.Application;

public sealed class SummaryService(ISummaryProjection projection)
{
    public async Task<DailySummary> GetAsync(DateOnly date, CancellationToken ct)
    {
        var (start, end) = RomeDay.Bounds(date);
        var activities = await projection.FindAsync(start, end, ct);
        return DailySummary.Calculate(date, start, end, activities);
    }

    public Task ApplyAsync(ActivityRecordedV1 message, CancellationToken ct)
    {
        var validDetails = message.Kind switch
        {
            "milk" => message.Milliliters is >= 1 and <= 500 && message.EndedAt is null,
            "diaper" => message.Milliliters is null && message.EndedAt is null,
            "sleep" => message.Milliliters is null && message.EndedAt > message.StartedAt,
            _ => false
        };

        if (message.ActivityId == Guid.Empty || message.StartedAt == default || !validDetails)
            throw new InvalidDataException("L'evento contiene un'attività non valida.");

        return projection.ApplyRecordedAsync(new SummaryActivity(
            message.ActivityId, message.Kind, message.StartedAt, message.EndedAt, message.Milliliters), ct);
    }

    public Task ApplyAsync(ActivityDeletedV1 message, CancellationToken ct)
    {
        if (message.ActivityId == Guid.Empty)
            throw new InvalidDataException("L'evento non contiene un identificativo valido.");

        return projection.ApplyDeletedAsync(message.ActivityId, ct);
    }
}
