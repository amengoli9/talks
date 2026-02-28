# Dependency Injection in .NET: Anti-Pattern e Pitfall

Guida completa con esempi pratici basata sulla documentazione ufficiale Microsoft.

---

## 1. Captive Dependency (Dipendenza Cattiva)

**Il problema:** Un servizio con lifetime più lungo (es. Singleton) cattura un servizio con lifetime più corto (es. Scoped o Transient). Il servizio "prigioniero" sopravvive ben oltre il suo ciclo di vita previsto.

Termine coniato da Mark Seemann — si riferisce alla misconfiguration dei lifetime dove un servizio a lunga vita tiene in ostaggio uno a vita breve.

### ❌ Anti-Pattern

```csharp
// === Registrazione ===
var services = new ServiceCollection();
services.AddSingleton<OrderService>();   // Singleton: vive per tutta l'app
services.AddScoped<DbContext>();          // Scoped: dovrebbe vivere per una request

// === Implementazione ===
public class OrderService
{
    private readonly DbContext _db; // ⚠️ Captive dependency!

    public OrderService(DbContext db)
    {
        _db = db; // Questo DbContext NON verrà mai rinnovato
    }

    public void CreateOrder(Order order)
    {
        _db.Orders.Add(order);
        _db.SaveChanges(); // Dopo un po', il DbContext è stale/corrotto
    }
}
```

**Cosa succede:** `OrderService` è singleton → viene creato una sola volta. Il `DbContext` iniettato alla creazione resta lo stesso per tutta la vita dell'applicazione, anche se era registrato come Scoped. Risultato: tracking di entità inconsistente, dati stale, possibili eccezioni.

### ✅ Soluzione

```csharp
// Opzione A: IServiceScopeFactory per creare scope on-demand
public class OrderService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void CreateOrder(Order order)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        db.Orders.Add(order);
        db.SaveChanges();
    } // DbContext viene disposto correttamente qui
}

// Opzione B: Allineare i lifetime (entrambi Scoped)
services.AddScoped<OrderService>();
services.AddScoped<DbContext>();

// Opzione C: Abilitare la validazione scope (in Development)
var serviceProvider = services.BuildServiceProvider(
    validateScopes: true // Lancia InvalidOperationException se rileva captive dependencies
);
// Messaggio: "Cannot consume scoped service 'Bar' from singleton 'Foo'."
```

**Regola d'oro dei lifetime:**
```
Singleton → può dipendere solo da → Singleton
Scoped    → può dipendere da     → Scoped, Singleton
Transient → può dipendere da     → Transient, Scoped, Singleton
```

---

## 2. Transient Disposable catturati dal Container

**Il problema:** Quando registri un servizio Transient che implementa `IDisposable`, il container DI mantiene un riferimento a ogni istanza creata per poterla disporre alla fine. Se risolvi dal root container (non da uno scope), queste istanze si accumulano → **memory leak**.

### ❌ Anti-Pattern

```csharp
public class ExpensiveResource : IDisposable
{
    private readonly byte[] _buffer = new byte[1024 * 1024]; // 1MB

    public void DoWork() => Console.WriteLine("Working...");

    public void Dispose()
    {
        Console.WriteLine($"{nameof(ExpensiveResource)} disposed");
    }
}

// Registrazione come Transient
services.AddTransient<ExpensiveResource>();

// Risoluzione dal root provider (NO scope!) — MEMORY LEAK
var provider = services.BuildServiceProvider();

for (int i = 0; i < 1000; i++)
{
    var resource = provider.GetRequiredService<ExpensiveResource>();
    resource.DoWork();
    // ⚠️ Ogni istanza viene trattenuta dal container!
    // 1000 istanze × 1MB = ~1GB di memoria non rilasciata
}
// Le 1000 istanze verranno disposte solo quando il provider viene disposto (shutdown app)
```

### ✅ Soluzione

```csharp
// Soluzione A: Usare uno scope per ogni unità di lavoro
for (int i = 0; i < 1000; i++)
{
    using var scope = provider.CreateScope();
    var resource = scope.ServiceProvider.GetRequiredService<ExpensiveResource>();
    resource.DoWork();
} // Ogni scope dispone le sue istanze transient alla fine del blocco using

// Soluzione B: Factory pattern — gestione manuale del ciclo di vita
services.AddSingleton<Func<ExpensiveResource>>(
    sp => () => new ExpensiveResource()
);

public class MyService
{
    private readonly Func<ExpensiveResource> _resourceFactory;

    public MyService(Func<ExpensiveResource> resourceFactory)
    {
        _resourceFactory = resourceFactory;
    }

    public void Process()
    {
        using var resource = _resourceFactory(); // Tu gestisci il Dispose
        resource.DoWork();
    }
}

// Soluzione C: Non implementare IDisposable se non serve davvero
// Se il servizio non ha risorse unmanaged, evita IDisposable
```

**Nota importante:** Il receiver di una dipendenza `IDisposable` NON deve chiamare `Dispose()` su quella dipendenza. È il container che si occupa del cleanup.

---

## 3. Service Locator Pattern

**Il problema:** Iniettare `IServiceProvider` e chiamare `GetService<T>()` all'interno della classe. Questo nasconde le dipendenze, rende impossibile capire cosa serve alla classe guardando il costruttore, e complica i test.

### ❌ Anti-Pattern

```csharp
public class NotificationService
{
    private readonly IServiceProvider _provider;

    public NotificationService(IServiceProvider provider) // ⚠️ Service Locator!
    {
        _provider = provider;
    }

    public void SendNotification(string userId, string message)
    {
        // Le dipendenze reali sono nascoste nel metodo
        var emailSender = _provider.GetRequiredService<IEmailSender>();
        var logger = _provider.GetRequiredService<ILogger<NotificationService>>();
        var userRepo = _provider.GetRequiredService<IUserRepository>();

        var user = userRepo.GetById(userId);
        emailSender.Send(user.Email, message);
        logger.LogInformation("Notification sent to {UserId}", userId);
    }
}

// Test: com'è complicato? Devi mockare un intero IServiceProvider!
[Fact]
public void SendNotification_ShouldWork()
{
    var mockProvider = new Mock<IServiceProvider>();
    mockProvider.Setup(p => p.GetService(typeof(IEmailSender)))
                .Returns(new Mock<IEmailSender>().Object);
    mockProvider.Setup(p => p.GetService(typeof(ILogger<NotificationService>)))
                .Returns(new Mock<ILogger<NotificationService>>().Object);
    // ... e così via per ogni dipendenza nascosta
}
```

### ✅ Soluzione

```csharp
public class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;
    private readonly IUserRepository _userRepo;

    // ✅ Dipendenze esplicite, visibili, testabili
    public NotificationService(
        IEmailSender emailSender,
        ILogger<NotificationService> logger,
        IUserRepository userRepo)
    {
        _emailSender = emailSender;
        _logger = logger;
        _userRepo = userRepo;
    }

    public void SendNotification(string userId, string message)
    {
        var user = _userRepo.GetById(userId);
        _emailSender.Send(user.Email, message);
        _logger.LogInformation("Notification sent to {UserId}", userId);
    }
}

// Test: semplice e diretto
[Fact]
public void SendNotification_ShouldSendEmail()
{
    var mockEmail = new Mock<IEmailSender>();
    var sut = new NotificationService(
        mockEmail.Object,
        Mock.Of<ILogger<NotificationService>>(),
        Mock.Of<IUserRepository>());

    sut.SendNotification("user1", "Hello");

    mockEmail.Verify(e => e.Send(It.IsAny<string>(), "Hello"), Times.Once);
}
```

**Ricorda:** la DI è un'*alternativa* ai pattern di accesso statico/globale. Se la mischi con `IServiceProvider` iniettato ovunque, stai vanificando i benefici dell'inversione delle dipendenze.

---

## 4. Async DI Factory → Deadlock

**Il problema:** Il container DI di .NET non supporta factory asincrone. Se usi `.Result` o `.GetAwaiter().GetResult()` in una factory, rischi un deadlock.

### ❌ Anti-Pattern

```csharp
services.AddSingleton<IConnection>(sp =>
{
    // ⚠️ .Result su un Task dentro una factory DI = DEADLOCK!
    return CreateConnectionAsync(sp).Result;
});

async Task<IConnection> CreateConnectionAsync(IServiceProvider sp)
{
    await Task.Delay(100); // Simula operazione asincrona
    var config = sp.GetRequiredService<IConfiguration>();
    var connection = new SqlConnection(config.GetConnectionString("Default"));
    await connection.OpenAsync();
    return connection;
}
```

**Perché deadlock?** La factory viene chiamata durante la risoluzione del servizio, che avviene in modo sincrono. Chiamare `.Result` blocca il thread in attesa del Task, ma il Task potrebbe aver bisogno dello stesso thread per completarsi (dipende dal `SynchronizationContext`).

### ✅ Soluzione

```csharp
// Soluzione A: Lazy async initialization
public class ConnectionWrapper
{
    private readonly Lazy<Task<IConnection>> _connection;

    public ConnectionWrapper(IConfiguration config)
    {
        _connection = new Lazy<Task<IConnection>>(async () =>
        {
            var conn = new SqlConnection(config.GetConnectionString("Default"));
            await conn.OpenAsync();
            return conn;
        });
    }

    public Task<IConnection> GetConnectionAsync() => _connection.Value;
}

services.AddSingleton<ConnectionWrapper>();

// Soluzione B: Inizializzare prima della registrazione
var builder = Host.CreateApplicationBuilder(args);

// Esegui l'inizializzazione asincrona PRIMA della build
var connection = await CreateConnectionAsync(builder.Configuration);
builder.Services.AddSingleton<IConnection>(connection);

// Soluzione C: IHostedService per inizializzazione asincrona
public class ConnectionInitializer : IHostedService
{
    private readonly ConnectionWrapper _wrapper;

    public ConnectionInitializer(ConnectionWrapper wrapper)
    {
        _wrapper = wrapper;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _wrapper.GetConnectionAsync(); // "Scalda" la connessione all'avvio
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## 5. Scoped Service usato come Singleton (accidentalmente)

**Il problema:** Se risolvi un servizio Scoped dal root `IServiceProvider` (senza creare uno scope), ottieni di fatto un singleton. Il servizio viene creato una volta e riutilizzato ovunque.

### ❌ Anti-Pattern

```csharp
services.AddScoped<ShoppingCart>(); // Dovrebbe essere per-request

var provider = services.BuildServiceProvider();

// ⚠️ Risolvere Scoped dal root provider = diventa singleton
var cart1 = provider.GetRequiredService<ShoppingCart>();
var cart2 = provider.GetRequiredService<ShoppingCart>();

Console.WriteLine(ReferenceEquals(cart1, cart2)); // TRUE! Stessa istanza!
// Tutti gli utenti condividono lo stesso carrello!
```

### ✅ Soluzione

```csharp
// Sempre creare uno scope per risolvere servizi Scoped
using (var scope1 = provider.CreateScope())
{
    var cart1 = scope1.ServiceProvider.GetRequiredService<ShoppingCart>();
    // cart1 è unico per questo scope
}

using (var scope2 = provider.CreateScope())
{
    var cart2 = scope2.ServiceProvider.GetRequiredService<ShoppingCart>();
    // cart2 è un'istanza diversa da cart1
}

// In ASP.NET Core, ogni HTTP request crea automaticamente uno scope,
// quindi i servizi Scoped sono per-request. 
// Ma nei background service / console app devi gestire gli scope manualmente.
```

---

## 6. Registrazioni Multiple: l'ultima vince (silenziosamente)

**Il problema:** Se registri la stessa interfaccia più volte con `AddSingleton`/`AddScoped`/`AddTransient`, il container NON lancia errore. L'ultima registrazione sovrascrive le precedenti quando risolvi un singolo servizio. Le precedenti restano disponibili solo via `IEnumerable<T>`. Questo può succedere facilmente quando componi molte extension method da librerie diverse.

### ❌ Anti-Pattern: Override silenzioso

```csharp
var builder = WebApplication.CreateBuilder(args);

// In Program.cs — il tuo team registra il servizio
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();

// Più avanti, o in una extension method di una libreria...
builder.Services.AddSingleton<IMessageWriter, LoggingMessageWriter>();
// ⚠️ Nessun errore! ConsoleMessageWriter è stato silenziosamente sostituito

var app = builder.Build();

// Quando risolvi un singolo servizio → ottieni SOLO l'ultimo registrato
var writer = app.Services.GetRequiredService<IMessageWriter>();
Console.WriteLine(writer.GetType().Name); // "LoggingMessageWriter" — sorpresa!
```

### ❌ Anti-Pattern: IEnumerable vs singolo — comportamento incoerente

```csharp
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
builder.Services.AddSingleton<IMessageWriter, LoggingMessageWriter>();

public class ExampleService
{
    public ExampleService(
        IMessageWriter messageWriter,                   // ← ultimo registrato
        IEnumerable<IMessageWriter> messageWriters)     // ← TUTTI i registrati
    {
        // Singolo: è LoggingMessageWriter (l'ultimo)
        Trace.Assert(messageWriter is LoggingMessageWriter);

        // Enumerable: contiene TUTTI, in ordine di registrazione
        var all = messageWriters.ToArray();
        Trace.Assert(all[0] is ConsoleMessageWriter);  // primo registrato
        Trace.Assert(all[1] is LoggingMessageWriter);  // secondo registrato
    }
}
```

Questo comportamento è **by design** ma sorprende molti sviluppatori. Se il tuo codice dipende da un singolo `IMessageWriter`, non hai alcun avviso che qualcun altro l'ha sovrascritto.

### ✅ Soluzione: TryAdd per registrazioni sicure

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

// TryAdd registra SOLO se il service type non è già presente
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
builder.Services.TryAddSingleton<IMessageWriter, LoggingMessageWriter>();
// ↑ Non ha effetto! IMessageWriter è già registrato

var writer = app.Services.GetRequiredService<IMessageWriter>();
Console.WriteLine(writer.GetType().Name); // "ConsoleMessageWriter" — il primo vince

// Per IEnumerable con implementazioni diverse, usa TryAddEnumerable
// che controlla anche il tipo di implementazione (non solo il service type)
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IMessageWriter, ConsoleMessageWriter>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IMessageWriter, LoggingMessageWriter>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IMessageWriter, ConsoleMessageWriter>()); // ← ignorata! Già presente
```

### Riepilogo comportamento `Add` vs `TryAdd`

```
Add<T, Impl>()        → aggiunge SEMPRE (l'ultimo vince per singolo, tutti per IEnumerable)
TryAdd<T, Impl>()     → aggiunge solo se T non è già registrato
TryAddEnumerable(...)  → aggiunge solo se la coppia (T, Impl) non è già presente
```

**Consiglio per library author:** Usate sempre `TryAdd` nelle vostre extension method `AddMyLibrary()`. Così il consumatore può fare override prima di voi senza conflitti.

---

## 7. Keyed Services: il pattern e le sue trappole

I Keyed Services (introdotti in .NET 8) permettono di registrare più implementazioni della stessa interfaccia distinguendole con una chiave. Prima dovevi usare factory, `IEnumerable<T>` + LINQ, o container di terze parti. Ora è nativo.

### Registrazione base e risoluzione

```csharp
// === Registrazione con chiave ===
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("memory");
builder.Services.AddKeyedScoped<ICache, SqlCache>("sql");

// === Risoluzione via constructor injection ===
public class ProductService
{
    private readonly ICache _cache;

    public ProductService(
        [FromKeyedServices("redis")] ICache cache) // specifica quale implementazione
    {
        _cache = cache; // È RedisCache
    }
}

// === In Minimal API ===
app.MapGet("/products", ([FromKeyedServices("memory")] ICache cache) =>
{
    return cache.Get("products");
});

// === In Middleware ===
public class CachingMiddleware
{
    public CachingMiddleware(RequestDelegate next) { }

    // Keyed services supportati sia nel costruttore che in Invoke
    public async Task InvokeAsync(
        HttpContext context,
        [FromKeyedServices("redis")] ICache cache)
    {
        // ...
    }
}

// === In Blazor ===
[Inject(Key = "redis")]
public ICache Cache { get; set; }
```

### AnyKey: registrazione fallback

`KeyedService.AnyKey` permette di registrare un'implementazione di default che risponde a qualsiasi chiave non registrata esplicitamente.

```csharp
// "premium" ha la sua implementazione specifica
builder.Services.AddKeyedSingleton<ICache, PremiumCache>("premium");

// Qualsiasi altra chiave usa DefaultCache come fallback
builder.Services.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);

// Risoluzione
var premium = provider.GetRequiredKeyedService<ICache>("premium");  // → PremiumCache
var basic   = provider.GetRequiredKeyedService<ICache>("basic");    // → DefaultCache (fallback)
var foo     = provider.GetRequiredKeyedService<ICache>("anything"); // → DefaultCache (fallback)
```

### ❌ Pitfall: Keyed vs Non-Keyed sono mondi separati

```csharp
// ⚠️ Keyed e non-keyed sono registrazioni completamente indipendenti!
builder.Services.AddSingleton<ICache, GlobalCache>();                // Non-keyed
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");     // Keyed

public class MyService
{
    public MyService(
        ICache defaultCache,                           // → GlobalCache (non-keyed)
        [FromKeyedServices("redis")] ICache redisCache) // → RedisCache (keyed)
    {
        // Sono DUE registrazioni separate, non si sovrascrivono
    }
}
```

### ❌ Pitfall: Chiave sbagliata con AnyKey fallback = bug silenzioso

```csharp
builder.Services.AddKeyedSingleton<ICache, PremiumCache>("premium");
builder.Services.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);

// Typo nella chiave! "premiun" invece di "premium"
public class MyService(
    [FromKeyedServices("premiun")] ICache cache) // ⚠️ Ottieni DefaultCache, non PremiumCache!
{
    // Nessun errore a runtime — il fallback AnyKey copre il typo
    // Bug silenziosissimo
}
```

### ❌ Pitfall: Chiave sbagliata SENZA fallback = eccezione solo a runtime

```csharp
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");
// NESSUN AnyKey fallback

public class MyService(
    [FromKeyedServices("reddis")] ICache cache) // ⚠️ Typo!
{
    // InvalidOperationException a runtime:
    // "No service for type 'ICache' has been registered."
    // Almeno qui il bug è visibile, ma solo a runtime!
}
```

### ✅ Best Practice per Keyed Services

```csharp
// 1. Usa costanti o enum per le chiavi — mai stringhe sparse!
public static class CacheKeys
{
    public const string Redis = "redis";
    public const string Memory = "memory";
    public const string Sql = "sql";
}

builder.Services.AddKeyedSingleton<ICache, RedisCache>(CacheKeys.Redis);
builder.Services.AddKeyedSingleton<ICache, MemoryCache>(CacheKeys.Memory);

public class MyService(
    [FromKeyedServices(CacheKeys.Redis)] ICache cache) // ✅ Refactoring-safe
{ }

// 2. Abilita ValidateOnBuild per catturare errori alla build
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// 3. La chiave non deve essere per forza string — può essere qualsiasi object
// che implementi Equals correttamente (int, enum, record, etc.)
public enum CacheType { Redis, Memory, Sql }

builder.Services.AddKeyedSingleton<ICache, RedisCache>(CacheType.Redis);
```

---

## 8. 🚨 Breaking Change .NET 10: GetKeyedService con AnyKey

Questa è una **behavioral change** in .NET 10 che può rompere codice esistente scritto per .NET 8/9.

### Il cambiamento

In .NET 10, il comportamento di `GetKeyedService()` e `GetKeyedServices()` con `KeyedService.AnyKey` è stato corretto per allinearsi alla semantica prevista:

**`GetKeyedService()` (singolare) con AnyKey → ora lancia eccezione**
**`GetKeyedServices()` (plurale) con AnyKey → non restituisce più le registrazioni AnyKey**

### Comportamento precedente (.NET 8/9)

```csharp
builder.Services.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);
builder.Services.AddKeyedSingleton<ICache, PremiumCache>("premium");

var provider = builder.Build().Services;

// .NET 8/9: funzionava — restituiva DefaultCache
var service = provider.GetKeyedService<ICache>(KeyedService.AnyKey);
Console.WriteLine(service!.GetType().Name); // "DefaultCache"

// .NET 8/9: restituiva TUTTE le registrazioni (incluse quelle AnyKey)
var all = provider.GetKeyedServices<ICache>(KeyedService.AnyKey);
Console.WriteLine(all.Count()); // 2 (DefaultCache + PremiumCache)
```

### Comportamento nuovo (.NET 10)

```csharp
builder.Services.AddKeyedSingleton<ICache, DefaultCache>(KeyedService.AnyKey);
builder.Services.AddKeyedSingleton<ICache, PremiumCache>("premium");

var provider = builder.Build().Services;

// .NET 10: BOOM! 💥
var service = provider.GetKeyedService<ICache>(KeyedService.AnyKey);
// InvalidOperationException: "Cannot resolve a single service using AnyKey."

// .NET 10: restituisce SOLO i servizi registrati con chiavi specifiche
var all = provider.GetKeyedServices<ICache>(KeyedService.AnyKey);
Console.WriteLine(all.Count()); // 1 (solo PremiumCache)
// DefaultCache (registrata con AnyKey) NON viene inclusa
```

### Perché questo cambiamento?

`AnyKey` è concepito come un **caso speciale** (un wildcard per la registrazione fallback), non come una chiave concreta. Usarlo per risolvere un singolo servizio era semanticamente sbagliato e portava a comportamenti confusi.

### ✅ Come aggiornare il codice per .NET 10

```csharp
// ❌ PRIMA (.NET 8/9) — Non funziona più in .NET 10
var fallback = provider.GetKeyedService<ICache>(KeyedService.AnyKey);

// ✅ DOPO (.NET 10) — Usa una chiave specifica
// Opzione A: Registra con una chiave esplicita per il default
builder.Services.AddKeyedSingleton<ICache, DefaultCache>("default");
var fallback = provider.GetKeyedService<ICache>("default");

// Opzione B: Usa registrazione non-keyed per il default
builder.Services.AddSingleton<ICache, DefaultCache>();           // non-keyed
builder.Services.AddKeyedSingleton<ICache, PremiumCache>("premium"); // keyed
var fallback = provider.GetService<ICache>();                     // → DefaultCache

// ❌ PRIMA (.NET 8/9) — Enumerava tutto incluse le AnyKey
var all = provider.GetKeyedServices<ICache>(KeyedService.AnyKey);

// ✅ DOPO (.NET 10) — GetKeyedServices con AnyKey restituisce solo chiavi specifiche
// Se vuoi davvero TUTTI i servizi (inclusi i non-keyed), usa approcci separati
var keyed = provider.GetKeyedServices<ICache>(KeyedService.AnyKey);  // solo chiavi specifiche
var nonKeyed = provider.GetService<ICache>();                         // il non-keyed
```

---

## Riepilogo: Tabella di riferimento rapido

| # | Anti-Pattern | Rischio | Soluzione |
|---|-------------|---------|-----------|
| 1 | Captive Dependency | Stato stale, comportamento scorretto | Allineare lifetime, `IServiceScopeFactory` |
| 2 | Transient Disposable dal root | Memory leak | Usare scope, factory pattern |
| 3 | Service Locator | Dipendenze nascoste, test difficili | Constructor injection esplicita |
| 4 | Async factory con `.Result` | Deadlock | Lazy init, `IHostedService` |
| 5 | Scoped senza scope | Singleton accidentale | Creare scope esplicitamente |
| 6 | Registrazioni multiple | Override silenzioso | `TryAdd`, `TryAddEnumerable` |
| 7 | Keyed Services — typo chiave | Bug silenzioso o eccezione runtime | Costanti/enum per le chiavi |
| 8 | AnyKey in .NET 10 | `InvalidOperationException` | Chiavi esplicite, registrazioni non-keyed |

---

## Tip: abilitare tutte le validazioni in Development

```csharp
var builder = WebApplication.CreateBuilder(args);

// In ASP.NET Core, queste validazioni sono abilitate di default in Development!
// Per console app, abilitale esplicitamente:
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;    // Rileva captive dependencies
    options.ValidateOnBuild = true;   // Verifica che tutti i servizi siano risolvibili al build
});
```

> **Fonti:**
> - [Microsoft Learn — Dependency injection guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines)
> - [Microsoft Learn — Service registration](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/service-registration)
> - [Microsoft Learn — Keyed services](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/overview#keyed-services)
> - [Microsoft Learn — .NET 10 Breaking Change: GetKeyedService with AnyKey](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/getkeyedservice-anykey)
