using System.Diagnostics;
using Kitchen.Api.Domain;
using Kitchen.Api.Utilities;
using RestaurantDemo;

namespace Kitchen.Api.Infrastructure;

// One immutable snapshot lets orders read while the background worker refreshes it.
public sealed class AvailabilityCache(ILogger<AvailabilityCache> logger) : BackgroundService, IAvailability
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefreshAhead = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private volatile Snapshot snapshot = new(Stock(), DateTimeOffset.UtcNow + Lifetime);
    private volatile bool proactive;
    private volatile bool intermittent;
    private volatile int managementDelayMs = 50;
    private int refreshCount;

    public object State => new
    {
        proactive, intermittent, managementDelayMs, expiresAt = snapshot.ExpiresAt,
        refreshCount = Volatile.Read(ref refreshCount)
    };

    public async Task<bool> IsAvailableAsync(string dish, CancellationToken ct)
    {
        using var activity = Telemetry.Source.StartActivity("kitchen.check_availability");
        if (intermittent)
        {
            activity?.SetTag("availability.mode", "intermittent");
            try { return await CheckIntermittentAsync(dish, ct); }
            catch (ManagementUnavailableException exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.AddException(exception);
                throw;
            }
        }
        var current = snapshot;
        var hit = current.ExpiresAt > DateTimeOffset.UtcNow;
        activity?.SetTag("cache.hit", hit);
        if (!hit)
        {
            logger.AvailabilityCacheExpired();
            await refreshLock.WaitAsync(ct);
            try
            {
                // Concurrent orders share the same refresh, rather than loading again.
                if (snapshot.ExpiresAt <= DateTimeOffset.UtcNow)
                    await RefreshAsync("request", ct);
                current = snapshot;
            }
            finally { refreshLock.Release(); }
        }
        return current.Availability.GetValueOrDefault(dish);
    }

    private async Task<bool> CheckIntermittentAsync(string dish, CancellationToken ct)
    {
        // Each order contacts the simulated management system independently. No cache
        // hit or shared refresh lock can hide failures or queue several slow requests.
        using var activity = Telemetry.Source.StartActivity("availability.lookup");
        var behavior = Random.Shared.Next(4) switch { 0 or 1 => "fast", 2 => "slow", _ => "error" };
        // Leave a little headroom below five seconds for HTTP and rendering overhead.
        var delayMs = behavior == "slow" ? Random.Shared.Next(2000, 4501) : 50;
        activity?.SetTag("management.behavior", behavior);
        activity?.SetTag("management.delay_ms", delayMs);
        logger.ManagementAvailabilityChecked(behavior, delayMs);
        await Task.Delay(delayMs, ct);
        if (behavior == "error")
        {
            var exception = new ManagementUnavailableException();
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            logger.ManagementUnavailable(exception);
            throw exception;
        }
        return Stock().GetValueOrDefault(dish);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var ticks = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await ticks.WaitForNextTickAsync(stoppingToken))
            {
                if (!proactive || snapshot.ExpiresAt - DateTimeOffset.UtcNow > RefreshAhead)
                    continue;
                await refreshLock.WaitAsync(stoppingToken);
                try
                {
                    if (proactive && snapshot.ExpiresAt - DateTimeOffset.UtcNow <= RefreshAhead)
                        await RefreshAsync("background", stoppingToken);
                }
                finally { refreshLock.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RefreshAsync(string trigger, CancellationToken ct)
    {
        using var activity = Telemetry.Source.StartActivity("availability.reload");
        activity?.SetTag("refresh.trigger", trigger);
        activity?.SetTag("management.delay_ms", managementDelayMs);
        logger.AvailabilityReloading(trigger, managementDelayMs);
        // Simulated management system I/O. No thread is blocked and no third API is needed.
        await Task.Delay(managementDelayMs, ct);
        snapshot = new Snapshot(Stock(), DateTimeOffset.UtcNow + Lifetime);
        Interlocked.Increment(ref refreshCount);
        logger.AvailabilityRefreshed(trigger, Lifetime.TotalSeconds);
    }

    public async Task SetScenarioAsync(string scenario, CancellationToken ct)
    {
        await refreshLock.WaitAsync(ct);
        try
        {
            intermittent = scenario == "intermittent";
            if (scenario == "proactive")
            {
                managementDelayMs = 10_000;
                // A long explanation may have outlived the snapshot. Warm it once before
                // enabling refresh-ahead; never pretend expired availability is fresh.
                if (snapshot.ExpiresAt <= DateTimeOffset.UtcNow)
                    await RefreshAsync("warmup", ct);
                snapshot = snapshot with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(20) };
                proactive = true;
            }
            else
            {
                proactive = false;
                managementDelayMs = scenario == "slow" ? 10_000 : 50;
                snapshot = new Snapshot(Stock(), scenario == "slow" ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow + Lifetime);
            }
            logger.DemoScenarioSelected(scenario);
        }
        finally { refreshLock.Release(); }
    }

    // Fixed demo stock: tiramisu is sold out, independently of cache performance.
    private static Dictionary<string, bool> Stock() => Menu.Items.ToDictionary(
        dish => dish.Id, dish => dish.Id != "tiramisu");
    private sealed record Snapshot(Dictionary<string, bool> Availability, DateTimeOffset ExpiresAt);
}

