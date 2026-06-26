using Polly;
using Polly.Retry;
using Polly.Simmy;
using Polly.Simmy.Fault;

// CHAOS ENGINEERING (holistic · continual · dynamic) con Polly v8 + Simmy.
// Iniettiamo guasti nel forno nel 50% dei casi: il retry deve recuperare.
// È la fitness function che chiede "la resilienza regge davvero?".

Console.WriteLine("Forno della piadineria — ci mettiamo il casino apposta (Simmy).\n");

var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
        MaxRetryAttempts = 5,
        Delay = TimeSpan.Zero,
        OnRetry = args =>
        {
            Console.WriteLine($"   ↻ il forno fa i capricci, ritento (tentativo {args.AttemptNumber + 1})...");
            return default;
        }
    })
    .AddChaosFault(new ChaosFaultStrategyOptions
    {
        InjectionRate = 0.5,
        FaultGenerator = static _ =>
            new ValueTask<Exception?>(new InvalidOperationException("forno spento"))
    })
    .Build();

for (var i = 1; i <= 8; i++)
{
    var numero = i;
    pipeline.Execute(() => Console.WriteLine($"🥙 piada #{numero} sfornata"));
}

Console.WriteLine("\nTutte le piade servite: il sistema regge al casino del forno.");
