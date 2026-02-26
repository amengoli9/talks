using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

Console.WriteLine("=== 6. REGISTRAZIONI MULTIPLE: L'ULTIMA VINCE ===\n");

// ========================
// ❌ ANTI-PATTERN: Override silenzioso
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- ANTI-PATTERN: L'ultima registrazione vince ---");
Console.ResetColor();

var services1 = new ServiceCollection();
services1.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
services1.AddSingleton<IMessageWriter, LoggingMessageWriter>(); // Sovrascrive!

var provider1 = services1.BuildServiceProvider();
var writer1 = provider1.GetRequiredService<IMessageWriter>();
Console.WriteLine($"  Singolo: {writer1.GetType().Name}");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ ConsoleMessageWriter e' stato silenziosamente sostituito!\n");
Console.ResetColor();

// ========================
// ❌ IEnumerable vs singolo
// ========================
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("--- IEnumerable vs Singolo: comportamento diverso ---");
Console.ResetColor();

var services2 = new ServiceCollection();
services2.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
services2.AddSingleton<IMessageWriter, LoggingMessageWriter>();
services2.AddSingleton<IMessageWriter, FileMessageWriter>();

var provider2 = services2.BuildServiceProvider();

var single = provider2.GetRequiredService<IMessageWriter>();
Console.WriteLine($"  GetRequiredService<T>(): {single.GetType().Name}");

var all = provider2.GetRequiredService<IEnumerable<IMessageWriter>>();
Console.WriteLine($"  IEnumerable<T>: [{string.Join(", ", all.Select(w => w.GetType().Name))}]");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Singolo = ultimo registrato. IEnumerable = TUTTI in ordine.\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: TryAdd
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: TryAdd — il primo vince ---");
Console.ResetColor();

var services3 = new ServiceCollection();
services3.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
services3.TryAddSingleton<IMessageWriter, LoggingMessageWriter>(); // Ignorata!

var provider3 = services3.BuildServiceProvider();
var writer3 = provider3.GetRequiredService<IMessageWriter>();
Console.WriteLine($"  TryAdd dopo Add: {writer3.GetType().Name}");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ LoggingMessageWriter ignorato, IMessageWriter gia' registrato\n");
Console.ResetColor();

// ========================
// ✅ SOLUZIONE: TryAddEnumerable
// ========================
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("--- SOLUZIONE: TryAddEnumerable — evita duplicati ---");
Console.ResetColor();

var services4 = new ServiceCollection();
services4.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageWriter, ConsoleMessageWriter>());
services4.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageWriter, LoggingMessageWriter>());
services4.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageWriter, ConsoleMessageWriter>()); // Dup!

var provider4 = services4.BuildServiceProvider();
var all4 = provider4.GetRequiredService<IEnumerable<IMessageWriter>>();
Console.WriteLine($"  TryAddEnumerable: [{string.Join(", ", all4.Select(w => w.GetType().Name))}]");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ^ Il duplicato ConsoleMessageWriter viene ignorato!\n");
Console.ResetColor();

Console.WriteLine("Riepilogo:");
Console.WriteLine("  Add<T, Impl>()        -> aggiunge SEMPRE (ultimo vince per singolo)");
Console.WriteLine("  TryAdd<T, Impl>()     -> aggiunge solo se T non e' registrato");
Console.WriteLine("  TryAddEnumerable(...) -> aggiunge solo se (T, Impl) non e' presente");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n  Consiglio: nelle extension method delle librerie, usate sempre TryAdd!");
Console.ResetColor();

// === Tipi ===

interface IMessageWriter
{
    string Write(string message);
}

class ConsoleMessageWriter : IMessageWriter
{
    public string Write(string message) => $"[Console] {message}";
}

class LoggingMessageWriter : IMessageWriter
{
    public string Write(string message) => $"[Logging] {message}";
}

class FileMessageWriter : IMessageWriter
{
    public string Write(string message) => $"[File] {message}";
}
