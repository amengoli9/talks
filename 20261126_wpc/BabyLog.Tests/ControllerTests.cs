using BabyLog.Contracts;
using BabyLog.Controllers;
using BabyLog.Domain;
using BabyLog.Infrastructure;
using BabyLog.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BabyLog.Tests;

public sealed class ControllerTests
{
    [Fact]
    public async Task Controller_returns_created_validation_problem_and_not_found_using_real_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new BabyLogDbContext(new DbContextOptionsBuilder<BabyLogDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var controller = new BabyLogController(new BabyLogService(new ActivityRepository(db)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var invalid = await controller.Create(new("milk", DateTimeOffset.UtcNow, Milliliters: 0), default);
        var problem = Assert.IsType<ObjectResult>(invalid.Result);
        Assert.Equal(400, problem.StatusCode);
        Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Empty(await db.Activities.ToListAsync());
        var valid = await controller.Create(new("milk", DateTimeOffset.Parse("2026-11-26T08:00:00Z"), Milliliters: 120), default);
        var created = Assert.IsType<CreatedResult>(valid.Result);
        var entry = Assert.IsType<ActivityEntry>(created.Value);
        Assert.Equal($"/api/activities/{entry.Id}", created.Location);
        Assert.Equal(120, (await controller.Summary(new DateOnly(2026, 11, 26), default)).Value!.MilkMilliliters);
        Assert.IsType<NoContentResult>(await controller.Delete(entry.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Delete(entry.Id, default));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("0001-01-01")]
    [InlineData("9999-12-31")]
    public async Task Date_queries_reject_missing_or_unsupported_days(string? input)
    {
        var controller = new BabyLogController(new BabyLogService(null!))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        DateOnly? date = input is null ? null : DateOnly.Parse(input);
        Assert.Equal(400, Assert.IsType<ObjectResult>((await controller.List(date, default)).Result).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>((await controller.Summary(date, default)).Result).StatusCode);
    }
}
