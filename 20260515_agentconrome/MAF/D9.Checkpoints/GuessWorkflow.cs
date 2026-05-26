using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;

namespace D9.Checkpoints;

internal static class Tel
{
    // Shared ActivitySource for every executor in this project so they show up as
    // spans under the "D9.Checkpoints" source captured by Telemetry.ConfigureConsole.
    internal static readonly ActivitySource Source = new("D9.Checkpoints");
}

internal enum NumberSignal
{
    Init,
    TooHigh,
    TooLow,
}

/// <summary>
/// A number-guessing workflow: a GuessExecutor and a JudgeExecutor wired in a
/// feedback loop. Each round is one "super step", and the framework saves a
/// checkpoint at the end of every super step when a CheckpointManager is supplied.
/// </summary>
internal static class GuessWorkflow
{
    internal static Workflow Build()
    {
        GuessExecutor guess = new(low: 1, high: 100);
        JudgeExecutor judge = new(target: 42);

        return new WorkflowBuilder(guess)
            .AddEdge(guess, judge)
            .AddEdge(judge, guess)
            .WithOutputFrom(judge)
            .Build();
    }
}

/// <summary>Makes a binary-search guess based on the current bounds.</summary>
[SendsMessage(typeof(int))]
internal sealed class GuessExecutor() : Executor<NumberSignal>("Guess")
{
    public int Low { get; private set; }
    public int High { get; private set; }

    private const string StateKey = "guess-state";

    public GuessExecutor(int low, int high) : this()
    {
        this.Low = low;
        this.High = high;
    }

    private int NextGuess => (this.Low + this.High) / 2;

    public override async ValueTask HandleAsync(NumberSignal message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.Guess");
        span?.SetTag("signal", message.ToString());
        span?.SetTag("bounds.low", this.Low);
        span?.SetTag("bounds.high", this.High);

        switch (message)
        {
            case NumberSignal.TooHigh:
                this.High = this.NextGuess - 1;
                break;
            case NumberSignal.TooLow:
                this.Low = this.NextGuess + 1;
                break;
        }

        span?.SetTag("guess", this.NextGuess);
        await context.SendMessageAsync(this.NextGuess, cancellationToken: cancellationToken);
    }

    // Persist this executor's mutable state into the checkpoint...
    protected override ValueTask OnCheckpointingAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        context.QueueStateUpdateAsync(StateKey, (this.Low, this.High), cancellationToken: cancellationToken);

    // ...and restore it when the workflow is resumed or rehydrated from a checkpoint.
    protected override async ValueTask OnCheckpointRestoredAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        (this.Low, this.High) = await context.ReadStateAsync<(int, int)>(StateKey, cancellationToken: cancellationToken);
}

/// <summary>Judges a guess against the target number and reports the result.</summary>
[SendsMessage(typeof(NumberSignal))]
[YieldsOutput(typeof(string))]
internal sealed class JudgeExecutor() : Executor<int>("Judge")
{
    private readonly int _target;
    private int _tries;

    private const string StateKey = "judge-state";

    public JudgeExecutor(int target) : this() => this._target = target;

    public override async ValueTask HandleAsync(int guess, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using Activity? span = Tel.Source.StartActivity("executor.Judge");
        this._tries++;
        span?.SetTag("guess", guess);
        span?.SetTag("target", this._target);
        span?.SetTag("tries", this._tries);

        if (guess == this._target)
        {
            span?.SetTag("verdict", "found");
            await context.YieldOutputAsync($"Numero {this._target} trovato in {this._tries} tentativi!", cancellationToken);
        }
        else if (guess > this._target)
        {
            span?.SetTag("verdict", "too-high");
            await context.SendMessageAsync(NumberSignal.TooHigh, cancellationToken: cancellationToken);
        }
        else
        {
            span?.SetTag("verdict", "too-low");
            await context.SendMessageAsync(NumberSignal.TooLow, cancellationToken: cancellationToken);
        }
    }

    // Only the mutable state (_tries) is checkpointed; _target is set fresh by the factory.
    protected override ValueTask OnCheckpointingAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        context.QueueStateUpdateAsync(StateKey, this._tries, cancellationToken: cancellationToken);

    protected override async ValueTask OnCheckpointRestoredAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        this._tries = await context.ReadStateAsync<int>(StateKey, cancellationToken: cancellationToken);
}
