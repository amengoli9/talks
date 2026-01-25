using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using System;

namespace BenchmarkLog;

/// <summary>
/// Benchmark comparing three .NET logging approaches:
/// 1. Standard Logging - ILogger extension methods (LogInformation, LogWarning, etc.)
/// 2. Source-Generated Logging - [LoggerMessage] attribute (compile-time generated)
/// 3. High-Performance Logging - LoggerMessage.Define() with cached delegates
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class LoggingBenchmarks
{
    private ILogger<LoggingBenchmarks> _logger = null!;
    private int _itemId;
    private string _itemName = null!;
    private double _price;

    [GlobalSetup]
    public void Setup()
    {
        // Create a logger that writes to NullLogger (no actual I/O)
        // This isolates the logging overhead from actual write operations
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(NullLoggerProvider.Instance);
        });

        _logger = loggerFactory.CreateLogger<LoggingBenchmarks>();

        // Sample data for logging
        _itemId = 42;
        _itemName = "ProductX";
        _price = 99.99;
    }

    /// <summary>
    /// Standard logging using ILogger extension methods.
    /// - Parses message template on every call
    /// - Boxes value types (int, double) to object
    /// - Allocates params array for arguments
    /// </summary>
    [Benchmark(Baseline = true)]
    public void StandardLogging()
    {
        _logger.LogInformation(
            "Processing item {ItemId}: {ItemName} at price {Price}",
            _itemId,
            _itemName,
            _price);
    }

    /// <summary>
    /// Source-generated logging using [LoggerMessage] attribute.
    /// - Template is parsed at compile time
    /// - Strongly typed parameters (no boxing)
    /// - No params array allocation
    /// - Generates optimal code via source generator
    /// </summary>
    [Benchmark]
    public void SourceGeneratedLogging()
    {
        _logger.LogProcessingItem(_itemId, _itemName, _price);
    }

    /// <summary>
    /// High-performance logging using LoggerMessage.Define().
    /// - Template is parsed once at static initialization
    /// - Strongly typed parameters (no boxing)
    /// - Cached delegate avoids repeated allocations
    /// </summary>
    [Benchmark]
    public void HighPerformanceLogging()
    {
        _logger.LogProcessingItemHighPerf(_itemId, _itemName, _price);
    }

    /// <summary>
    /// Standard logging when log level is disabled.
    /// Shows overhead even when logging is "off"
    /// </summary>
    [Benchmark]
    public void StandardLogging_Disabled()
    {
        _logger.LogDebug(
            "Debug processing item {ItemId}: {ItemName} at price {Price}",
            _itemId,
            _itemName,
            _price);
    }

    /// <summary>
    /// Source-generated logging when log level is disabled.
    /// Generated code includes IsEnabled check
    /// </summary>
    [Benchmark]
    public void SourceGeneratedLogging_Disabled()
    {
        _logger.LogDebugProcessingItem(_itemId, _itemName, _price);
    }

    /// <summary>
    /// High-performance logging when log level is disabled.
    /// Delegate still gets invoked but returns early
    /// </summary>
    [Benchmark]
    public void HighPerformanceLogging_Disabled()
    {
        _logger.LogDebugProcessingItemHighPerf(_itemId, _itemName, _price);
    }
}

#region Source-Generated Logging (Compile-Time)

/// <summary>
/// Source-generated logging methods using [LoggerMessage] attribute.
/// The source generator creates the implementation at compile time.
/// </summary>
public static partial class SourceGeneratedLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Processing item {ItemId}: {ItemName} at price {Price}")]
    public static partial void LogProcessingItem(
        this ILogger logger,
        int itemId,
        string itemName,
        double price);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Debug processing item {ItemId}: {ItemName} at price {Price}")]
    public static partial void LogDebugProcessingItem(
        this ILogger logger,
        int itemId,
        string itemName,
        double price);
}

#endregion

#region High-Performance Logging (LoggerMessage.Define)

/// <summary>
/// High-performance logging using LoggerMessage.Define().
/// Delegates are cached in static fields and reused.
/// </summary>
public static class HighPerformanceLoggerExtensions
{
    // Cached delegate - template is parsed only once during static initialization
    private static readonly Action<ILogger, int, string, double, Exception?> s_processingItem =
        LoggerMessage.Define<int, string, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogProcessingItemHighPerf)),
            "Processing item {ItemId}: {ItemName} at price {Price}");

    private static readonly Action<ILogger, int, string, double, Exception?> s_debugProcessingItem =
        LoggerMessage.Define<int, string, double>(
            LogLevel.Debug,
            new EventId(2, nameof(LogDebugProcessingItemHighPerf)),
            "Debug processing item {ItemId}: {ItemName} at price {Price}");

    public static void LogProcessingItemHighPerf(
        this ILogger logger,
        int itemId,
        string itemName,
        double price)
    {
        s_processingItem(logger, itemId, itemName, price, null);
    }

    public static void LogDebugProcessingItemHighPerf(
        this ILogger logger,
        int itemId,
        string itemName,
        double price)
    {
        s_debugProcessingItem(logger, itemId, itemName, price, null);
    }
}

#endregion

#region Null Logger Provider (for benchmarking)

/// <summary>
/// A logger provider that creates loggers which discard all output.
/// Used to measure logging overhead without I/O costs.
/// </summary>
public sealed class NullLoggerProvider : ILoggerProvider
{
    public static readonly NullLoggerProvider Instance = new();

    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
    public void Dispose() { }
}

/// <summary>
/// A logger that discards all log messages but still processes them.
/// This lets us measure the overhead of logging itself.
/// </summary>
public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Simulate minimal work - format the message
        if (IsEnabled(logLevel))
        {
            _ = formatter(state, exception);
        }
    }
}

#endregion

#region Program Entry Point


#endregion