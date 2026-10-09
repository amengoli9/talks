using System.Text.Json;

namespace Torino.Live;

// I profili restano separati: passando a Ollama non si riusano deployment o chiavi Azure.
internal sealed record ModelSettings(string Provider, Uri Endpoint, string ApiKey,
    string Model, string? AttackModel, string? JudgeModel)
{
    public static ModelSettings Load(string? settingsPath = null, Func<string, string?>? environment = null)
    {
        settingsPath ??= Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        environment ??= Environment.GetEnvironmentVariable;
        using var settings = File.Exists(settingsPath)
            ? JsonDocument.Parse(File.ReadAllText(settingsPath)) : null;

        string? Read(string section, string property, string variable)
        {
            string? value = environment(variable);
            if (string.IsNullOrWhiteSpace(value) && settings is not null &&
                settings.RootElement.TryGetProperty(section, out var group) &&
                group.TryGetProperty(property, out var item))
                value = item.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        string Required(string property, string variable) => Read("AI", property, variable)
            ?? throw new InvalidOperationException(
                $"Configura AI:{property} in ../appsettings.local.json oppure {variable}. Vedi README.md.");

        string provider = (Read("AI", "Provider", "AI_PROVIDER") ?? "foundry").ToLowerInvariant();
        bool local = provider switch
        {
            "ollama" => true,
            "foundry" => false,
            _ => throw new InvalidOperationException("AI_PROVIDER deve essere foundry oppure ollama.")
        };
        string address = local
            ? Read("Ollama", "Endpoint", "OLLAMA_ENDPOINT") ?? "http://localhost:11434/v1/"
            : Required("Endpoint", "AI_ENDPOINT");
        if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps &&
             !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)))
            throw new ArgumentException($"{(local ? "OLLAMA_ENDPOINT" : "AI_ENDPOINT")} deve usare HTTPS, eccetto HTTP su localhost.");

        string model = local
            ? Read("Ollama", "Model", "OLLAMA_MODEL") ?? "qwen3:8b"
            : Required("Model", "AI_MODEL");
        return new(provider, endpoint, local ? "ollama" : Required("ApiKey", "AI_API_KEY"), model,
            local ? Read("Ollama", "AttackModel", "OLLAMA_ATTACK_MODEL") ?? model : Read("AI", "AttackModel", "AI_ATTACK_MODEL"),
            local ? Read("Ollama", "JudgeModel", "OLLAMA_JUDGE_MODEL") ?? model : Read("AI", "JudgeModel", "AI_JUDGE_MODEL"));
    }

    public string GetModel(string role) => role switch
    {
        "AttackModel" => AttackModel ?? throw new InvalidOperationException(
            "Configura AI:AttackModel in ../appsettings.local.json oppure AI_ATTACK_MODEL."),
        "JudgeModel" => JudgeModel ?? throw new InvalidOperationException(
            "Configura AI:JudgeModel in ../appsettings.local.json oppure AI_JUDGE_MODEL."),
        _ => throw new ArgumentException($"Ruolo del modello sconosciuto: {role}.", nameof(role))
    };
}
