namespace Torino.Live;

// Solo presentazione: nessuna dipendenza e nessun markup da interpretare nei testi del modello.
internal static class DemoConsole
{
    private static readonly object Sync = new();

    public static void Title(string title)
    {
        Line();
        Line(new string('─', 68), ConsoleColor.DarkCyan);
        Line("  AI DAY TORINO · MICROSOFT AGENT FRAMEWORK", ConsoleColor.Gray);
        Line($"  {title}", ConsoleColor.Cyan);
        Line(new string('─', 68), ConsoleColor.DarkCyan);
        Line();
    }

    public static void Section(string title, ConsoleColor color = ConsoleColor.Cyan)
    {
        Line();
        Line($"── {title} ──", color);
    }

    public static void Answer(string text)
    {
        Section("RISPOSTA", ConsoleColor.Green);
        Line(text, ConsoleColor.White);
        Line();
    }

    public static void Decision(string check, string outcome)
    {
        var color = outcome switch
        {
            "approved" or "allowed" or "clean" => ConsoleColor.Green,
            "blocked" or "denied" or "poisoned" => ConsoleColor.Red,
            _ => ConsoleColor.Yellow
        };
        Line($"[CONTROLLO] {check}: {outcome}", color);
    }

    public static void Line(string text = "", ConsoleColor color = ConsoleColor.Gray) =>
        Write(text, color, newLine: true);

    public static void Prompt(string text, ConsoleColor color = ConsoleColor.Cyan) =>
        Write(text, color, newLine: false);

    private static void Write(string text, ConsoleColor color, bool newLine)
    {
        lock (Sync)
        {
            // Log e pipe restano testo semplice. NO_COLOR=1 disattiva i colori anche nel terminale.
            bool useColor = !Console.IsOutputRedirected &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            ConsoleColor original = useColor ? Console.ForegroundColor : default;
            try
            {
                if (useColor) Console.ForegroundColor = color;
                if (newLine) Console.WriteLine(text);
                else Console.Write(text);
            }
            finally
            {
                if (useColor) Console.ForegroundColor = original;
            }
        }
    }
}
