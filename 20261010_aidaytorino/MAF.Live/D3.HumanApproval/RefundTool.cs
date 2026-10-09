using System.ComponentModel;
using Torino.Live;

public sealed class RefundTool
{
    public decimal Total { get; private set; }

    [Description("Simula un rimborso in euro. Nessun pagamento reale. Budget totale: 99 euro.")]
    public string Refund(decimal amount)
    {
        using var span = Telemetry.Source.StartActivity("tool.Refund");
        // Anche un sì umano NON può superare il budget. Vale anche per importi frazionati.
        if (amount <= 0 || amount > 99 - Total)
        {
            Telemetry.Decision("refund_budget", "blocked");
            return "Rimborso bloccato: importo non valido o budget totale di 99 EUR superato.";
        }
        Total += amount;
        Telemetry.Decision("refund_budget", "allowed");
        DemoConsole.Line($"[EFFETTO SIMULATO] Rimborso di {amount} EUR.", ConsoleColor.Magenta);
        return "Rimborso simulato completato.";
    }
}
