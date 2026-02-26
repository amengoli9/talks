using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 7. KEYED SERVICES: PATTERN E TRAPPOLE ===\n");

// ========================
// ✅ Registrazione base e risoluzione
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- Keyed Services: registrazione e risoluzione ---");
Console.ResetColor();

var services = new ServiceCollection();
services.AddKeyedSingleton<ICache, RedisCache>("redis");
services.AddKeyedSingleton<ICache, MemoryCache>("memory");

var provider = services.BuildServiceProvider();

var redis = provider.GetRequiredKeyedService<ICache>("redis");
var memory = provider.GetRequiredKeyedService<ICache>("memory");
Console.WriteLine($"  {redis.Get("products")}");
Console.WriteLine($"  {memory.Get("products")}\n");

// ========================
// ✅ AnyKey fallback
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- AnyKey: registrazione fallback ---");
Console.ResetColor();

var anyKeyServices = new ServiceCollection();
anyKeyServices.AddKeyedSingleton<ICache, PremiumCache>("premium");
anyKeyServices.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);

var anyKeyProvider = anyKeyServices.BuildServiceProvider();

var premium = anyKeyProvider.GetRequiredKeyedService<ICache>("premium");
var basic = anyKeyProvider.GetRequiredKeyedService<ICache>("basic");
var foo = anyKeyProvider.GetRequiredKeyedService<ICache>("anything");

Console.WriteLine($"  'premium':  {premium.GetType().Name}");
Console.WriteLine($"  'basic':    {basic.GetType().Name} (fallback)");
Console.WriteLine($"  'anything': {foo.GetType().Name} (fallback)\n");

// ========================
// ❌ PITFALL: Keyed vs Non-Keyed sono mondi separati
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- PITFALL: Keyed vs Non-Keyed = mondi separati ---");
Console.ResetColor();

var mixedServices = new ServiceCollection();
mixedServices.AddSingleton<ICache, GlobalCache>();
mixedServices.AddKeyedSingleton<ICache, RedisCache>("redis");

var mixedProvider = mixedServices.BuildServiceProvider();

var nonKeyed = mixedProvider.GetRequiredService<ICache>();
var keyed = mixedProvider.GetRequiredKeyedService<ICache>("redis");

Console.WriteLine($"  Non-keyed: {nonKeyed.GetType().Name}");
Console.WriteLine($"  Keyed 'redis': {keyed.GetType().Name}");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Sono DUE registrazioni indipendenti!\n");
Console.ResetColor();

// ========================
// ❌ PITFALL: Typo con AnyKey = bug silenzioso
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- PITFALL: Typo nella chiave con AnyKey fallback ---");
Console.ResetColor();

var typoServices = new ServiceCollection();
typoServices.AddKeyedSingleton<ICache, PremiumCache>("premium");
typoServices.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);

var typoProvider = typoServices.BuildServiceProvider();

var oops = typoProvider.GetRequiredKeyedService<ICache>("premiun"); // Typo!
Console.WriteLine($"  Richiesto 'premiun' (typo): {oops.GetType().Name}");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Bug silenzioso! Volevi PremiumCache, hai DefaultCache!\n");
Console.ResetColor();

// ========================
// ❌ PITFALL: Typo SENZA fallback
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- PITFALL: Typo senza fallback = eccezione a runtime ---");
Console.ResetColor();

var noFallbackServices = new ServiceCollection();
noFallbackServices.AddKeyedSingleton<ICache, RedisCache>("redis");

var noFallbackProvider = noFallbackServices.BuildServiceProvider();

try
{
    var boom = noFallbackProvider.GetRequiredKeyedService<ICache>("reddis");
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  Eccezione: {ex.Message}");
    Console.ResetColor();
}

// ========================
// ✅ BEST PRACTICE
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n--- BEST PRACTICE: Costanti per le chiavi ---");
Console.ResetColor();

Console.WriteLine("  public static class CacheKeys");
Console.WriteLine("  {");
Console.WriteLine("      public const string Redis = \"redis\";");
Console.WriteLine("      public const string Memory = \"memory\";");
Console.WriteLine("  }");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n  Usa costanti o enum per le chiavi — mai stringhe sparse!");
Console.WriteLine("  Abilita ValidateOnBuild per catturare errori al build time.");
Console.ResetColor();

// === Tipi ===

interface ICache
{
    string Get(string key);
}

class RedisCache : ICache
{
    public string Get(string key) => $"[Redis] value for '{key}'";
}

class MemoryCache : ICache
{
    public string Get(string key) => $"[Memory] value for '{key}'";
}

class DefaultCache : ICache
{
    public string Get(string key) => $"[Default/Fallback] value for '{key}'";
}

class PremiumCache : ICache
{
    public string Get(string key) => $"[Premium] value for '{key}'";
}

class GlobalCache : ICache
{
    public string Get(string key) => $"[Global/Non-Keyed] value for '{key}'";
}
