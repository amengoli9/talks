using BabyLog.Contracts;

namespace BabyLog.Domain;

public sealed class BabyLogService(IActivityRepository repository) : IBabyLogService
{
    public static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    public static bool IsSupportedDate(DateOnly date) => date > DateOnly.MinValue && date < DateOnly.MaxValue;
    public static (DateTimeOffset Start, DateTimeOffset End) Bounds(DateOnly date)
    {
        if (!IsSupportedDate(date)) throw new ArgumentOutOfRangeException(nameof(date), "La data deve essere compresa tra 0001-01-02 e 9999-12-30.");
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(start, Rome), TimeZoneInfo.ConvertTimeToUtc(start.AddDays(1), Rome));
    }
    public Task<IReadOnlyList<Activity>> ListAsync(DateOnly date, CancellationToken ct = default)
    {
        var (start, end) = Bounds(date);
        return repository.FindAsync(start, end, ct);
    }
    public async Task<Activity> CreateAsync(CreateActivityRequest request, CancellationToken ct = default)
    {
        var activity = Activity.Create(request.Kind, request.StartedAt, request.EndedAt, request.Milliliters, request.Notes);
        await repository.AddAsync(activity, ct);
        return activity;
    }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => repository.DeleteAsync(id, ct);
    public async Task<DailySummary> SummaryAsync(DateOnly date, CancellationToken ct = default)
    {
        var (start, end) = Bounds(date);
        var entries = await repository.FindAsync(start, end, ct);
        var sleepTicks = entries.Where(x => x.Kind == "sleep").Sum(x => (Min(x.EndedAt!.Value, end) - Max(x.StartedAt, start)).Ticks);
        return new(date, entries.Where(x => x.Kind == "milk").Sum(x => x.Milliliters ?? 0), entries.Count(x => x.Kind == "diaper"), (int)TimeSpan.FromTicks(sleepTicks).TotalMinutes);
    }
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
