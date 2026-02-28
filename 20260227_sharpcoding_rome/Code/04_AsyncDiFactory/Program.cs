using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 4. ASYNC DI FACTORY -> DEADLOCK ===\n");

// ========================
// ❌ ANTI-PATTERN: .Result in una factory DI
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: .Result in factory DI ---");
Console.ResetColor();

Console.WriteLine("  services.AddSingleton<IConnection>(sp =>");
Console.WriteLine("  {");
Console.WriteLine("      return CreateConnectionAsync(sp).Result; // DEADLOCK!");
Console.WriteLine("  });");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n  Il container DI risolve in modo sincrono.");
Console.WriteLine("  .Result blocca il thread -> il Task potrebbe aver bisogno");
Console.WriteLine("  dello stesso thread -> DEADLOCK!\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE A: Lazy async initialization
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE A: Lazy<Task<T>> wrapper ---");
Console.ResetColor();

var lazyServices = new ServiceCollection();
lazyServices.AddSingleton<ConnectionWrapper>();
var lazyProvider = lazyServices.BuildServiceProvider();

var wrapper = lazyProvider.GetRequiredService<ConnectionWrapper>();
var conn1 = await wrapper.GetConnectionAsync();
var result1 = await conn1.QueryAsync("SELECT * FROM Dishes");
Console.WriteLine(result1);

var conn2 = await wrapper.GetConnectionAsync();
Console.WriteLine($"  Stessa connessione? {ReferenceEquals(conn1, conn2)}\n");

// ========================
// ✅ SOLUZIONE B: Inizializzazione prima del build
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE B: Init async PRIMA della build ---");
Console.ResetColor();

Console.WriteLine("  var connection = await CreateConnectionAsync(config);");
Console.WriteLine("  builder.Services.AddSingleton<IConnection>(connection);");
Console.WriteLine("  // L'oggetto e' gia' pronto — nessun async nella factory\n");

var preInitConnection = new FakeConnection();
Console.WriteLine($"  Connessione pre-inizializzata: {preInitConnection.ConnectionId}");

var preInitServices = new ServiceCollection();
preInitServices.AddSingleton<IConnection>(preInitConnection);
var preInitProvider = preInitServices.BuildServiceProvider();
var resolved = preInitProvider.GetRequiredService<IConnection>();
Console.WriteLine($"  Risolta dal container: {resolved.ConnectionId}");
Console.WriteLine($"  Stessa istanza? {ReferenceEquals(preInitConnection, resolved)}\n");

// ========================
// ✅ SOLUZIONE C: IHostedService
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE C: IHostedService per warm-up ---");
Console.ResetColor();

Console.WriteLine("  public class ConnectionInitializer : IHostedService");
Console.WriteLine("  {");
Console.WriteLine("      public async Task StartAsync(CancellationToken ct)");
Console.WriteLine("      {");
Console.WriteLine("          await _wrapper.GetConnectionAsync();");
Console.WriteLine("      }");
Console.WriteLine("  }");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n  L'IHostedService viene eseguito all'avvio dell'host.");
Console.WriteLine("  Perfetto per inizializzazioni async!");
Console.ResetColor();

// === Tipi ===

interface IConnection
{
    string ConnectionId { get; }
    Task<string> QueryAsync(string sql);
}

class FakeConnection : IConnection
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString()[..8];
    public Task<string> QueryAsync(string sql)
        => Task.FromResult($"  [Connection {ConnectionId}] Result of: {sql}");
}

class ConnectionWrapper
{
    private readonly Lazy<Task<IConnection>> _connection;

    public ConnectionWrapper()
    {
        _connection = new Lazy<Task<IConnection>>(async () =>
        {
            Console.WriteLine("  [ConnectionWrapper] Inizializzazione async...");
            await Task.Delay(100);
            var conn = new FakeConnection();
            Console.WriteLine($"  [ConnectionWrapper] Connessione {conn.ConnectionId} creata");
            return conn;
        });
    }

    public Task<IConnection> GetConnectionAsync() => _connection.Value;
}
