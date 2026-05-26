// D9 - Checkpoints: save and resume a workflow's state.
//
// A number-guessing workflow runs in "super steps" (one move per step). With a
// CheckpointManager, the framework saves a checkpoint at the end of every super step;
// each executor persists its own state via OnCheckpointingAsync / OnCheckpointRestoredAsync.
//
// Then we REHYDRATE a brand-new workflow instance from a mid-run checkpoint and let it
// finish - proving the state was truly persisted, not just kept in memory.

using System.Diagnostics;
using D9.Checkpoints;
using Maf.Demo.Shared;
using Microsoft.Agents.AI.Workflows;

const string ServiceName = "D9.Checkpoints";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
using var activitySource = new ActivitySource(ServiceName);
using Activity? rootActivity = activitySource.StartActivity("INIT", ActivityKind.Client);

// ============================================================
// Run 1 - Execute the workflow, collecting one checkpoint per super step.
// ============================================================
Console.WriteLine("=== Esecuzione con un checkpoint a ogni super-step ===");

var checkpointManager = CheckpointManager.Default;
var checkpoints = new List<CheckpointInfo>();

Workflow workflow = GuessWorkflow.Build();
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, NumberSignal.Init, checkpointManager);

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent done:
            Console.WriteLine($"  executor '{done.ExecutorId}' completato");
            break;

        case SuperStepCompletedEvent step:
            CheckpointInfo? checkpoint = step.CompletionInfo?.Checkpoint;
            if (checkpoint is not null)
            {
                checkpoints.Add(checkpoint);
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  ** checkpoint #{checkpoints.Count} salvato (fine super-step)");
                Console.ResetColor();
            }
            break;

        case WorkflowOutputEvent output:
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  RISULTATO: {output.Data}");
            Console.ResetColor();
            break;

        case WorkflowErrorEvent error:
            Console.Error.WriteLine(error.Exception);
            break;
    }
}

Console.WriteLine($"\nTotale checkpoint salvati: {checkpoints.Count}\n");

if (checkpoints.Count == 0)
{
    return;
}

// ============================================================
// Run 2 - Rehydrate a BRAND-NEW workflow instance from a mid-run checkpoint.
// ============================================================
int index = checkpoints.Count / 2;
Console.WriteLine($"=== Reidrato un NUOVO workflow dal checkpoint #{index + 1} ===");

Workflow freshWorkflow = GuessWorkflow.Build();
await using StreamingRun resumed = await InProcessExecution.ResumeStreamingAsync(
    freshWorkflow, checkpoints[index], checkpointManager);

await foreach (WorkflowEvent evt in resumed.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent done:
            Console.WriteLine($"  executor '{done.ExecutorId}' completato");
            break;

        case WorkflowOutputEvent output:
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  RISULTATO (ripreso): {output.Data}");
            Console.ResetColor();
            break;

        case WorkflowErrorEvent error:
            Console.Error.WriteLine(error.Exception);
            break;
    }
}

Console.WriteLine("\nIl nuovo workflow ha ripreso dallo stato salvato nel checkpoint e ha completato il gioco.");
