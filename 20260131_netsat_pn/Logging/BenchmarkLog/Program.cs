using BenchmarkDotNet.Running;
using System;

namespace BenchmarkLog;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== .NET Logging Performance Benchmark ===");
        Console.WriteLine();
        Console.WriteLine("Comparing three logging approaches:");
        Console.WriteLine("1. Standard Logging     - ILogger.LogInformation() extension methods");
        Console.WriteLine("2. Source-Generated     - [LoggerMessage] attribute (compile-time)");
        Console.WriteLine("3. High-Performance     - LoggerMessage.Define() cached delegates");
        Console.WriteLine();
        Console.WriteLine("Documentation references:");
        Console.WriteLine("- https://learn.microsoft.com/dotnet/core/extensions/logging");
        Console.WriteLine("- https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator");
        Console.WriteLine("- https://learn.microsoft.com/dotnet/core/extensions/high-performance-logging");
        Console.WriteLine();

        var summary = BenchmarkRunner.Run<LoggingBenchmarks>();
    }
}