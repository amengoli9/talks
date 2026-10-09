using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI.Evaluation;
using Torino.Live;

internal static class EvaluationReport
{
    public static void Print(AgentEvaluationResults results, string? model, bool regression, string? judgeModel = null)
    {
        DemoConsole.Section("EVALUATION");
        DemoConsole.Line($"{results.ProviderName}: {results.Passed}/{results.Total} pass",
            results.AllPassed ? ConsoleColor.Green : ConsoleColor.Red);
        var rows = results.Items.Select((item, i) => new
        {
            Case = i + 1,
            Metrics = item.Metrics.Values.Select(m => new
            {
                m.Name,
                Score = (m as NumericMetric)?.Value,
                Rating = m.Interpretation?.Rating.ToString(),
                Failed = m.Interpretation?.Failed
            }).ToArray()
        }).ToArray();
        foreach (var row in rows)
        {
            DemoConsole.Line($"  Caso {row.Case}:", ConsoleColor.Cyan);
            foreach (var metric in row.Metrics)
                DemoConsole.Line($"    {metric.Name}={metric.Rating}", metric.Failed switch
                {
                    true => ConsoleColor.Red,
                    false => ConsoleColor.Green,
                    null => ConsoleColor.Yellow
                });
        }

        string directory = Path.Combine(AppContext.BaseDirectory, ".artifacts", "evals");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Timestamp = DateTimeOffset.UtcNow,
            TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString(),
            Framework = typeof(AIAgent).Assembly.GetName().Version?.ToString(),
            AgentModel = model, JudgeModel = judgeModel,
            DatasetVersion = "manual-input", PromptVersion = "event-v1", Regression = regression,
            results.ProviderName, results.Passed, results.Total, Rows = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        DemoConsole.Line($"Report (senza prompt/risposte): {path}");
    }
}
