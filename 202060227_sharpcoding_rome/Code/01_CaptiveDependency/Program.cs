using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 1. CAPTIVE DEPENDENCY ===\n");

// ========================
// ❌ ANTI-PATTERN DEMO
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: Singleton cattura Scoped ---");
Console.ResetColor();

var badServices = new ServiceCollection();
badServices.AddSingleton<BadOrderService>();   // Singleton
badServices.AddScoped<FakeDbContext>();         // Scoped — verrà "catturato"!

var badProvider = badServices.BuildServiceProvider();

// Simuliamo 3 "request" — il DbContext dovrebbe cambiare ogni volta
for (int i = 1; i <= 3; i++)
{
    using var scope = badProvider.CreateScope();
    var orderService = scope.ServiceProvider.GetRequiredService<BadOrderService>();
    orderService.CreateOrder($"Pizza #{i}");
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ PROBLEMA: Stesso DbContext per tutte le request!\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE DEMO
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: IServiceScopeFactory ---");
Console.ResetColor();

var goodServices = new ServiceCollection();
goodServices.AddSingleton<GoodOrderService>();
goodServices.AddScoped<FakeDbContext>();

var goodProvider = goodServices.BuildServiceProvider();

for (int i = 1; i <= 3; i++)
{
    var orderService = goodProvider.GetRequiredService<GoodOrderService>();
    orderService.CreateOrder($"Pizza #{i}");
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ CORRETTO: Nuovo DbContext per ogni operazione!\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: ValidateScopes
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: ValidateScopes rileva il problema ---");
Console.ResetColor();

var validateServices = new ServiceCollection();
validateServices.AddSingleton<BadOrderService>();
validateServices.AddScoped<FakeDbContext>();

var validateProvider = validateServices.BuildServiceProvider(validateScopes: true);

try
{
    using var scope = validateProvider.CreateScope();
    var orderService = scope.ServiceProvider.GetRequiredService<BadOrderService>();
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  Eccezione catturata: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("Regola d'oro dei lifetime:");
Console.WriteLine("  Singleton -> dipende solo da -> Singleton");
Console.WriteLine("  Scoped    -> dipende da      -> Scoped, Singleton");
Console.WriteLine("  Transient -> dipende da      -> Transient, Scoped, Singleton");

// === Tipi ===

class FakeDbContext
{
    public Guid InstanceId { get; } = Guid.NewGuid();

    public void SaveOrder(string item)
        => Console.WriteLine($"  [DbContext {InstanceId.ToString()[..8]}] Saving order: {item}");
}

class BadOrderService(FakeDbContext db)
{
    public void CreateOrder(string item)
    {
        Console.WriteLine($"  [BadOrderService] Using DbContext {db.InstanceId.ToString()[..8]}");
        db.SaveOrder(item);
    }
}

class GoodOrderService(IServiceScopeFactory scopeFactory)
{
    public void CreateOrder(string item)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FakeDbContext>();
        Console.WriteLine($"  [GoodOrderService] Using DbContext {db.InstanceId.ToString()[..8]}");
        db.SaveOrder(item);
    }
}
