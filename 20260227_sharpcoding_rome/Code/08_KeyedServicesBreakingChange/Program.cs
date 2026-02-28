using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 8. BREAKING CHANGE .NET 10: GetKeyedService con AnyKey ===\n");

// ========================
// Setup
// ========================
var services = new ServiceCollection();
services.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);
services.AddKeyedSingleton<ICache, PremiumCache>("premium");
services.AddKeyedSingleton<ICache, StandardCache>("standard");

var provider = services.BuildServiceProvider();

// ========================
// ❌ GetKeyedService con AnyKey
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- GetKeyedService(AnyKey) in .NET 10 ---");
Console.ResetColor();

Console.WriteLine("  // .NET 8/9: restituiva DefaultCache");
Console.WriteLine("  // .NET 10:  lancia InvalidOperationException!\n");

try
{
    var service = provider.GetKeyedService<ICache>(KeyedService.AnyKey);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  Risultato: {service?.GetType().Name ?? "null"} (.NET 8/9 behavior)");
    Console.ResetColor();
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  Eccezione (.NET 10): {ex.Message}");
    Console.ResetColor();
}

// ========================
// GetKeyedServices (plurale) con AnyKey
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("\n--- GetKeyedServices(AnyKey) in .NET 10 ---");
Console.ResetColor();

Console.WriteLine("  // .NET 8/9: restituiva TUTTE (incluse AnyKey)");
Console.WriteLine("  // .NET 10:  restituisce SOLO le chiavi specifiche\n");

var all = provider.GetKeyedServices<ICache>(KeyedService.AnyKey);
var count = all.Count();
var types = string.Join(", ", all.Select(s => s.GetType().Name));

Console.WriteLine($"  GetKeyedServices(AnyKey): {count} servizi -> [{types}]");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  .NET 8/9 -> 3 (DefaultCache + PremiumCache + StandardCache)");
Console.WriteLine("  .NET 10  -> 2 (solo PremiumCache + StandardCache)\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: Come migrare a .NET 10
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: Come migrare a .NET 10 ---");
Console.ResetColor();

Console.WriteLine("\n  Opzione A: Chiave esplicita per il default");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("    services.AddKeyedSingleton<ICache, DefaultCache>(\"default\");");
Console.WriteLine("    var fallback = provider.GetKeyedService<ICache>(\"default\");");
Console.ResetColor();

var servicesA = new ServiceCollection();
servicesA.AddKeyedSingleton<ICache, DefaultCache>("default");
servicesA.AddKeyedSingleton<ICache, PremiumCache>("premium");
var providerA = servicesA.BuildServiceProvider();
var fallbackA = providerA.GetRequiredKeyedService<ICache>("default");
Console.WriteLine($"    -> {fallbackA.GetType().Name}");

Console.WriteLine("\n  Opzione B: Registrazione non-keyed per il default");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("    services.AddSingleton<ICache, DefaultCache>();              // non-keyed");
Console.WriteLine("    services.AddKeyedSingleton<ICache, PremiumCache>(\"premium\"); // keyed");
Console.ResetColor();

var servicesB = new ServiceCollection();
servicesB.AddSingleton<ICache, DefaultCache>();
servicesB.AddKeyedSingleton<ICache, PremiumCache>("premium");
var providerB = servicesB.BuildServiceProvider();
var fallbackB = providerB.GetRequiredService<ICache>();
var premiumB = providerB.GetRequiredKeyedService<ICache>("premium");
Console.WriteLine($"    -> Default (non-keyed): {fallbackB.GetType().Name}");
Console.WriteLine($"    -> Premium (keyed):     {premiumB.GetType().Name}");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n  AnyKey e' un wildcard per la registrazione fallback,");
Console.WriteLine("  NON una chiave concreta per la risoluzione!");
Console.ResetColor();

// === Tipi ===

interface ICache
{
    string Get(string key);
}

class DefaultCache : ICache
{
    public string Get(string key) => $"[Default/AnyKey] value for '{key}'";
}

class PremiumCache : ICache
{
    public string Get(string key) => $"[Premium] value for '{key}'";
}

class StandardCache : ICache
{
    public string Get(string key) => $"[Standard] value for '{key}'";
}
