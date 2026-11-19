namespace BabyLog.Distributed.Summaries.Domain;

public sealed record SummaryActivity(
    Guid Id, string Kind, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int? Milliliters);

public sealed record DailySummary(DateOnly Date, int MilkMilliliters, int DiaperCount, int SleepMinutes)
{
    public static DailySummary Calculate(
        DateOnly date, DateTimeOffset start, DateTimeOffset end, IReadOnlyList<SummaryActivity> activities)
    {
        var milk = 0;
        var diapers = 0;
        var sleepTicks = 0L;

        foreach (var activity in activities)
        {
            switch (activity.Kind)
            {
                case "milk":
                    milk += activity.Milliliters ?? 0;
                    break;
                case "diaper":
                    diapers++;
                    break;
                case "sleep" when activity.EndedAt is { } endedAt:
                    var overlapStart = activity.StartedAt > start ? activity.StartedAt : start;
                    var overlapEnd = endedAt < end ? endedAt : end;
                    if (overlapEnd > overlapStart)
                        sleepTicks += (overlapEnd - overlapStart).Ticks;
                    break;
            }
        }

        return new(date, milk, diapers, (int)(sleepTicks / TimeSpan.TicksPerMinute));
    }
}
