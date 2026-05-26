// D11 - Sequential workflow whose nodes are pure C# function-style executors,
//       with ONE node that is itself a conditional sub-workflow.
//
// Pipeline:
//   Parse  ->  Enrich  ->  [Router sub-workflow: switch-case su importo]  ->  Finalize
//
// Niente agenti LLM: gli executor sono semplici funzioni C# (Executor<TIn, TOut>).
// Il router è un workflow a se' (con AddSwitch + AddCase + WithDefault) incorporato
// come un singolo executor tramite subWorkflow.BindAsExecutor(...).

using System.Diagnostics;
using D11.FunctionExecutors;
using Maf.Demo.Shared;
using Microsoft.Agents.AI.Workflows;

const string ServiceName = "D11.FunctionExecutors";

using TelemetryBundle telemetry = Telemetry.ConfigureConsole(ServiceName);
using var activitySource = new ActivitySource(ServiceName);
using Activity? rootActivity = activitySource.StartActivity("INIT", ActivityKind.Client);

Workflow workflow = OrderPipeline.Build();

string[] inputs =
[
    "ord-1|Mario Rossi|50",      // < 100   -> lane fast
    "ord-2|Anna Bianchi|500",    // 100..999 -> lane standard
    "ord-3|Luca Verdi|1500",     // >= 1000 -> lane audit
];

foreach (string input in inputs)
{
    Console.WriteLine(new string('=', 64));
    Console.WriteLine($"Input: {input}");
    Console.WriteLine(new string('-', 64));

    await using Run runOnce = await InProcessExecution.RunAsync(workflow, input);

    foreach (WorkflowEvent evt in runOnce.NewEvents)
    {
        switch (evt)
        {
            case WorkflowOutputEvent output:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"RISULTATO: {output.Data}");
                Console.ResetColor();
                break;

            case WorkflowErrorEvent error:
                Console.Error.WriteLine(error.Exception);
                break;

            case ExecutorFailedEvent failed:
                Console.Error.WriteLine($"Executor '{failed.ExecutorId}' fallito: {failed.Data}");
                break;
        }
    }

    Console.WriteLine();
}
