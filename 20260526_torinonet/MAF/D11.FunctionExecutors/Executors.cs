using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;

namespace D11.FunctionExecutors;

internal static class Tel
{
    // Shared ActivitySource for every executor so they show up as spans under the
    // "D11.FunctionExecutors" source captured by Telemetry.ConfigureConsole.
    internal static readonly ActivitySource Source = new("D11.FunctionExecutors");
}

/// <summary>The data that flows through the pipeline.</summary>
internal sealed record Order(string Id, string Customer, decimal Amount, string? Notes = null, string? Lane = null);

// ============================================================
// Outer pipeline executors (pure C#, NO agent involved)
// ============================================================

/// <summary>Parses a raw "id|customer|amount" string into an <see cref="Order"/>.</summary>
internal sealed class ParseExecutor() : Executor<string, Order>("Parse")
{
    public override ValueTask<Order> HandleAsync(string raw, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.Parse");
        span?.SetTag("input.raw", raw);

        string[] parts = raw.Split('|');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException($"Formato ordine non valido: '{raw}'. Atteso 'id|cliente|importo'.");
        }

        Order order = new(
            Id: parts[0].Trim(),
            Customer: parts[1].Trim(),
            Amount: decimal.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture));

        span?.SetTag("order.id", order.Id);
        span?.SetTag("order.amount", order.Amount);
        Console.WriteLine($"  [parse]    -> id={order.Id}, cliente={order.Customer}, importo={order.Amount}");
        return ValueTask.FromResult(order);
    }
}

/// <summary>Adds non-AI metadata to the order (timestamp, defaulted note).</summary>
internal sealed class EnrichExecutor() : Executor<Order, Order>("Enrich")
{
    public override ValueTask<Order> HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.Enrich");
        span?.SetTag("order.id", order.Id);

        Order enriched = order with { Notes = $"ricevuto {DateTimeOffset.Now:HH:mm:ss}" };
        span?.SetTag("order.notes", enriched.Notes);
        Console.WriteLine($"  [enrich]   -> notes='{enriched.Notes}'");
        return ValueTask.FromResult(enriched);
    }
}

/// <summary>Final step: formats the processed order as a string and yields it as the workflow output.</summary>
[YieldsOutput(typeof(string))]
internal sealed class FinalizeExecutor() : Executor<Order>("Finalize")
{
    public override async ValueTask HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.Finalize");
        span?.SetTag("order.id", order.Id);
        span?.SetTag("order.lane", order.Lane);

        string summary = $"ordine {order.Id} | cliente {order.Customer} | importo {order.Amount} | lane: {order.Lane} | {order.Notes}";
        Console.WriteLine($"  [finalize] -> {summary}");
        await context.YieldOutputAsync(summary, cancellationToken);
    }
}

// ============================================================
// Inner conditional sub-workflow executors
// ============================================================

/// <summary>Entry point of the sub-workflow: passes the order through so the switch can route it.</summary>
internal sealed class RouterEntryExecutor() : Executor<Order, Order>("RouterEntry")
{
    public override ValueTask<Order> HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.RouterEntry");
        span?.SetTag("order.id", order.Id);
        span?.SetTag("order.amount", order.Amount);

        Console.WriteLine($"  [router]   -> valuto importo {order.Amount} per scegliere la lane");
        return ValueTask.FromResult(order);
    }
}

internal sealed class SmallLaneExecutor() : Executor<Order, Order>("SmallLane")
{
    public override ValueTask<Order> HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.SmallLane");
        span?.SetTag("order.id", order.Id);
        span?.SetTag("lane", "fast");

        Console.WriteLine($"  [small]    -> ordine piccolo, lane = fast");
        return ValueTask.FromResult(order with { Lane = "fast" });
    }
}

internal sealed class StandardLaneExecutor() : Executor<Order, Order>("StandardLane")
{
    public override ValueTask<Order> HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.StandardLane");
        span?.SetTag("order.id", order.Id);
        span?.SetTag("lane", "standard");

        Console.WriteLine($"  [standard] -> ordine medio, lane = standard");
        return ValueTask.FromResult(order with { Lane = "standard" });
    }
}

internal sealed class LargeLaneExecutor() : Executor<Order, Order>("LargeLane")
{
    public override ValueTask<Order> HandleAsync(Order order, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.LargeLane");
        span?.SetTag("order.id", order.Id);
        span?.SetTag("lane", "audit");

        Console.WriteLine($"  [large]    -> ordine grosso, lane = audit (con controllo extra)");
        return ValueTask.FromResult(order with { Lane = "audit", Notes = $"{order.Notes} | revisione manuale richiesta" });
    }
}

// ============================================================
// Workflow factory
// ============================================================

internal static class OrderPipeline
{
    /// <summary>
    /// Builds the inner conditional sub-workflow: a switch-case on the order amount
    /// routes to one of three lane handlers (small / standard / large).
    /// </summary>
    private static Workflow BuildRouterSubWorkflow()
    {
        RouterEntryExecutor entry = new();
        SmallLaneExecutor small = new();
        StandardLaneExecutor standard = new();
        LargeLaneExecutor large = new();

        return new WorkflowBuilder(entry)
            .AddSwitch(entry, sb => sb
                .AddCase<Order>(o => o is { Amount: < 100m }, small)
                .AddCase<Order>(o => o is { Amount: >= 1000m }, large)
                .WithDefault(standard))
            .WithOutputFrom(small, standard, large)
            .Build();
    }

    /// <summary>
    /// Builds the outer sequential pipeline:
    ///   Parse -> Enrich -> [conditional router sub-workflow] -> Finalize
    /// </summary>
    internal static Workflow Build()
    {
        ParseExecutor parse = new();
        EnrichExecutor enrich = new();
        FinalizeExecutor finalize = new();

        // The conditional sub-workflow is embedded as a single executor in the outer pipeline.
        ExecutorBinding router = BuildRouterSubWorkflow().BindAsExecutor("Router");

        return new WorkflowBuilder(parse)
            .AddEdge(parse, enrich)
            .AddEdge(enrich, router)
            .AddEdge(router, finalize)
            .WithOutputFrom(finalize)
            .Build();
    }
}
