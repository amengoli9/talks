using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Torino.Live;

// Solo plumbing. La costruzione dell'agente resta in ogni Program.cs.
internal static class Demo
{
    private static bool _promptHelpShown;

    public static IChatClient CreateClient(string? modelOverride = null)
    {
        var settings = ModelSettings.Load();
        string model = modelOverride ?? settings.Model;
        DemoConsole.Line($"Modello: {settings.Provider} · {model}", ConsoleColor.Cyan);
        var client = new OpenAIClient(new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = settings.Endpoint })
            .GetChatClient(model).AsIChatClient();

        // Una span per OGNI chiamata al modello, anche per il giudice delle evaluation.
        return new OpenTelemetryChatClient(client, sourceName: Telemetry.ModelSource)
        {
            EnableSensitiveData = true // Messaggi visibili nel visualizzatore GenAI di Aspire.
        };
    }

    public static string GetModel(string role) => ModelSettings.Load().GetModel(role);

    public static string? ReadPrompt(params string[] examples)
    {
        if (!_promptHelpShown)
        {
            DemoConsole.Section("IL TUO PROMPT");
            DemoConsole.Line("Esempi da copiare · /esci per uscire");
            foreach (string example in examples) DemoConsole.Line($"  › {example}");
            _promptHelpShown = true;
        }

        while (true)
        {
            DemoConsole.Prompt("\nTu > ");
            string? input = Console.ReadLine();
            if (input is null || input.Equals("/esci", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrWhiteSpace(input)) return input;
        }
    }
}
