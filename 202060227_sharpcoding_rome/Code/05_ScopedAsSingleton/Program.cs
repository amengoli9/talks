using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== 5. SCOPED SERVICE USATO COME SINGLETON ===\n");

// ========================
// ❌ ANTI-PATTERN: Scoped risolto dal root provider
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: Scoped dal root provider = singleton! ---");
Console.ResetColor();

var badServices = new ServiceCollection();
badServices.AddScoped<ShoppingCart>();

var badProvider = badServices.BuildServiceProvider();

var cart1 = badProvider.GetRequiredService<ShoppingCart>();
cart1.AddItem("Pizza");

var cart2 = badProvider.GetRequiredService<ShoppingCart>();
cart2.AddItem("Pasta");

Console.WriteLine($"  cart1: {cart1}");
Console.WriteLine($"  cart2: {cart2}");
Console.WriteLine($"  Stessa istanza? {ReferenceEquals(cart1, cart2)}");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ PROBLEMA: Tutti gli utenti condividono lo stesso carrello!\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: Creare scope esplicitamente
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: Creare scope per ogni unita di lavoro ---");
Console.ResetColor();

var goodServices = new ServiceCollection();
goodServices.AddScoped<ShoppingCart>();

var goodProvider = goodServices.BuildServiceProvider();

for (int i = 1; i <= 3; i++)
{
    using var scope = goodProvider.CreateScope();
    var cart = scope.ServiceProvider.GetRequiredService<ShoppingCart>();
    cart.AddItem($"Item-{i}a");
    cart.AddItem($"Item-{i}b");
    Console.WriteLine($"  Request {i}: {cart}");
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ CORRETTO: Ogni scope ha il suo carrello indipendente!\n");
Console.ResetColor();

// ========================
// ✅ ValidateScopes cattura il problema
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- ValidateScopes rileva il problema ---");
Console.ResetColor();

var validateServices = new ServiceCollection();
validateServices.AddScoped<ShoppingCart>();

var validateProvider = validateServices.BuildServiceProvider(validateScopes: true);

try
{
    var cart = validateProvider.GetRequiredService<ShoppingCart>();
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  Eccezione: {ex.Message}");
    Console.ResetColor();
}

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n  In ASP.NET Core, ogni HTTP request crea automaticamente uno scope.");
Console.WriteLine("  Nei background service e console app devi gestire gli scope manualmente!");
Console.ResetColor();

// === Tipi ===

class ShoppingCart
{
    public Guid CartId { get; } = Guid.NewGuid();
    private readonly List<string> _items = [];

    public void AddItem(string item) => _items.Add(item);
    public int ItemCount => _items.Count;

    public override string ToString()
        => $"Cart {CartId.ToString()[..8]}: {ItemCount} item(s) [{string.Join(", ", _items)}]";
}
