using BabyLog.Contracts;
using BabyLog.Domain;
using BabyLog.Infrastructure;
using BabyLog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BabyLog.Tests;

public sealed class ActivityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-26T09:00:00+01:00");
    [Theory]
    [InlineData("milk", 1)]
    [InlineData("milk", 500)]
    [InlineData("diaper", null)]
    [InlineData("sleep", null)]
    public void Valid_activity_is_normalized_to_utc(string kind, int? milk)
    {
        var activity = Activity.Create(kind, Start, kind == "sleep" ? Start.AddMinutes(60) : null, milk, " nota ");
        Assert.NotEqual(Guid.Empty, activity.Id);
        Assert.Equal(TimeSpan.Zero, activity.StartedAt.Offset);
        Assert.Equal(Start.UtcDateTime, activity.StartedAt.UtcDateTime);
        Assert.Equal("nota", activity.Notes);
    }
    [Theory]
    [InlineData("milk", 0, false)]
    [InlineData("milk", 501, false)]
    [InlineData("milk", null, false)]
    [InlineData("diaper", 20, false)]
    [InlineData("diaper", null, true)]
    [InlineData("sleep", null, false)]
    [InlineData("unknown", null, false)]
    public void Invalid_activity_is_rejected(string kind, int? milk, bool end) => Assert.Throws<ArgumentException>(() => Activity.Create(kind, Start, end ? Start.AddHours(1) : null, milk, null));
    [Fact]
    public void Invalid_sleep_and_long_notes_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Activity.Create("sleep", Start, Start, null, null));
        Assert.Throws<ArgumentException>(() => Activity.Create("sleep", Start, Start.AddMinutes(-1), null, null));
        Assert.Throws<ArgumentException>(() => Activity.Create("diaper", Start, null, null, new string('x', 201)));
    }
    [Fact]
    public void Rome_days_account_for_daylight_saving()
    {
        var spring = BabyLogService.Bounds(new DateOnly(2026, 3, 29));
        var autumn = BabyLogService.Bounds(new DateOnly(2026, 10, 25));
        Assert.Equal(23, (spring.End - spring.Start).TotalHours);
        Assert.Equal(25, (autumn.End - autumn.Start).TotalHours);
    }
    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("9999-12-31")]
    public void Calendar_bounds_reject_days_that_cannot_be_safely_converted(string input) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BabyLogService.Bounds(DateOnly.Parse(input)));
    [Fact]
    public async Task Sqlite_persists_events_clips_sleep_at_Rome_midnight_and_deletes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"babylog-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BabyLogDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        Guid milkId;
        try
        {
            await using (var db = new BabyLogDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                var service = new BabyLogService(new ActivityRepository(db));
                milkId = (await service.CreateAsync(new("milk", DateTimeOffset.Parse("2026-11-25T23:15:00Z"), Milliliters: 90))).Id;
                await service.CreateAsync(new("diaper", DateTimeOffset.Parse("2026-11-26T22:59:59Z")));
                await service.CreateAsync(new("diaper", DateTimeOffset.Parse("2026-11-26T23:00:00Z")));
                await service.CreateAsync(new("sleep", DateTimeOffset.Parse("2026-11-25T22:30:00Z"), DateTimeOffset.Parse("2026-11-25T23:45:00Z")));
                await service.CreateAsync(new("sleep", DateTimeOffset.Parse("2026-11-26T22:30:00Z"), DateTimeOffset.Parse("2026-11-27T00:00:00Z")));
            }
            await using (var db = new BabyLogDbContext(options))
            {
                var service = new BabyLogService(new ActivityRepository(db));
                Assert.Equal(new DailySummary(new(2026, 11, 26), 90, 1, 75), await service.SummaryAsync(new(2026, 11, 26)));
                Assert.Equal(4, (await service.ListAsync(new(2026, 11, 26))).Count);
                Assert.Equal(60, (await service.SummaryAsync(new(2026, 11, 27))).SleepMinutes);
                Assert.True(await service.DeleteAsync(milkId));
                Assert.False(await service.DeleteAsync(milkId));
                Assert.Equal(0, (await service.SummaryAsync(new(2026, 11, 26))).MilkMilliliters);
            }
        }
        finally { File.Delete(path); }
    }
}
