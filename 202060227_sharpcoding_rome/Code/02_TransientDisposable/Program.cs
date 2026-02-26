using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 2. TRANSIENT DISPOSABLE CATTURATI DAL CONTAINER ===\n");

// ========================
// ❌ ANTI-PATTERN: Transient IDisposable dal root provider
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: Transient Disposable senza scope ---");
Console.ResetColor();

ExpensiveResource.ResetCounter();
var badServices = new ServiceCollection();
badServices.AddTransient<ExpensiveResource>();
var badProvider = badServices.BuildServiceProvider();

Console.WriteLine("  Creo 5 istanze dal root provider...");
for (int i = 0; i < 5; i++)
{
    var resource = badProvider.GetRequiredService<ExpensiveResource>();
    resource.DoWork();
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Nessun Dispose! Le 5 istanze restano in memoria.");
Console.WriteLine("  Il Dispose avviene solo quando il provider viene disposto:\n");
Console.ResetColor();

((IDisposable)badProvider).Dispose();
Console.WriteLine();

// ========================
// ✅ SOLUZIONE: Usare scope per ogni unita di lavoro
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: Scope per ogni unita di lavoro ---");
Console.ResetColor();

ExpensiveResource.ResetCounter();
var goodServices = new ServiceCollection();
goodServices.AddTransient<ExpensiveResource>();
var goodProvider = goodServices.BuildServiceProvider();

Console.WriteLine("  Creo 5 istanze, ciascuna in uno scope...");
for (int i = 0; i < 5; i++)
{
    using var scope = goodProvider.CreateScope();
    var resource = scope.ServiceProvider.GetRequiredService<ExpensiveResource>();
    resource.DoWork();
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Ogni risorsa viene disposta subito alla fine dello scope!\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: Factory pattern
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: Factory pattern (gestione manuale) ---");
Console.ResetColor();

ExpensiveResource.ResetCounter();
var factoryServices = new ServiceCollection();
factoryServices.AddSingleton<Func<ExpensiveResource>>(() => new ExpensiveResource());
var factoryProvider = factoryServices.BuildServiceProvider();

var factory = factoryProvider.GetRequiredService<Func<ExpensiveResource>>();

for (int i = 0; i < 3; i++)
{
    using var resource = factory();
    resource.DoWork();
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Il container non trattiene nulla — gestisci tu il ciclo di vita!");
Console.ResetColor();

// === Tipi ===

class ExpensiveResource : IDisposable
{
    private static int _instanceCount;
    public int Id { get; } = Interlocked.Increment(ref _instanceCount);

    public void DoWork() => Console.WriteLine($"  [Resource #{Id}] Working...");
    public void Dispose() => Console.WriteLine($"  [Resource #{Id}] Disposed!");
    public static void ResetCounter() => _instanceCount = 0;
}
