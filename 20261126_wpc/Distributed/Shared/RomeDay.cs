namespace BabyLog.Distributed;

internal static class RomeDay
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static bool IsSupported(DateOnly date) => date > DateOnly.MinValue && date < DateOnly.MaxValue;

    public static (DateTimeOffset Start, DateTimeOffset End) Bounds(DateOnly date)
    {
        if (!IsSupported(date))
            throw new ArgumentOutOfRangeException(nameof(date), "La data deve essere compresa tra 0001-01-02 e 9999-12-30.");

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(start, Rome), TimeZoneInfo.ConvertTimeToUtc(start.AddDays(1), Rome));
    }
}
