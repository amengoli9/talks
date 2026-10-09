using Microsoft.Agents.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Torino.Live;

internal static class QualityJudge
{
    public static async Task<AgentEvaluationResults> EvaluateAsync(AIAgent agent,
        AgentEvaluationResults results, string judgeModel, CancellationToken token)
    {
        using var judge = Demo.CreateClient(judgeModel);
        var items = results.InputItems ?? throw new InvalidOperationException("Risposte da valutare non disponibili.");
        var responses = items.Select(item => new AgentResponse(item.Split().ResponseMessages.ToList())).ToArray();
        // Questo overload valuta risposte già ottenute, senza rieseguire l'agente o i tool.
        return await agent.EvaluateAsync(responses, items.Select(item => item.Query),
            new CompositeEvaluator(new RelevanceEvaluator(), new CoherenceEvaluator()),
            chatConfiguration: new ChatConfiguration(judge), cancellationToken: token);
    }
}
