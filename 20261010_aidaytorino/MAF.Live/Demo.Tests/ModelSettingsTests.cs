using Torino.Live;
using Xunit;

namespace Torino.Live.Tests;

public sealed class ModelSettingsTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"torino-settings-{Guid.NewGuid():N}.json");
    private const string Profiles = """
        {
          "AI": {
            "Endpoint": "https://foundry.example/openai/v1/",
            "Model": "cloud-agent", "ApiKey": "cloud-test-key",
            "AttackModel": "cloud-attack", "JudgeModel": "cloud-judge"
          },
          "Ollama": {
            "Endpoint": "http://localhost:11434/v1/", "Model": "local-agent"
          }
        }
        """;

    private ModelSettings Load(string? json, params (string Key, string Value)[] variables)
    {
        if (json is not null) File.WriteAllText(path, json);
        var values = variables.ToDictionary(v => v.Key, v => v.Value);
        return ModelSettings.Load(path, key => values.GetValueOrDefault(key));
    }

    [Fact]
    public void ExistingSettingsDefaultToFoundry()
    {
        var settings = Load(Profiles);
        Assert.Equal("foundry", settings.Provider);
        Assert.Equal("https://foundry.example/openai/v1/", settings.Endpoint.AbsoluteUri);
        Assert.Equal("cloud-test-key", settings.ApiKey);
        Assert.Equal("cloud-agent", settings.Model);
        Assert.Equal("cloud-attack", settings.GetModel("AttackModel"));
        Assert.Equal("cloud-judge", settings.GetModel("JudgeModel"));
    }

    [Fact]
    public void FileSwitchUsesLocalProfileEvenWithCloudEnvironmentOverrides()
    {
        var settings = Load(Profiles.Replace("\"AI\": {", "\"AI\": { \"Provider\": \"ollama\","),
            ("AI_ENDPOINT", "https://other-cloud.example/v1/"), ("AI_MODEL", "other-cloud"),
            ("AI_API_KEY", "other-cloud-key"), ("AI_ATTACK_MODEL", "other-attack"),
            ("AI_JUDGE_MODEL", "other-judge"));
        Assert.Equal("ollama", settings.Provider);
        Assert.Equal("http://localhost:11434/v1/", settings.Endpoint.AbsoluteUri);
        Assert.Equal("ollama", settings.ApiKey);
        Assert.Equal("local-agent", settings.Model);
        Assert.Equal("local-agent", settings.GetModel("AttackModel"));
        Assert.Equal("local-agent", settings.GetModel("JudgeModel"));
    }

    [Fact]
    public void OllamaWorksWithoutAnyCloudSettings()
    {
        var settings = Load("""
            { "AI": { "Provider": "OLLAMA" }, "Ollama": {
                "Model": "local-agent", "AttackModel": "local-attack", "JudgeModel": "local-judge"
            } }
            """);
        Assert.Equal("local-agent", settings.Model);
        Assert.Equal("local-attack", settings.GetModel("AttackModel"));
        Assert.Equal("local-judge", settings.GetModel("JudgeModel"));
    }

    [Fact]
    public void LocalEnvironmentOverridesLocalFileValues()
    {
        var settings = Load(Profiles, ("AI_PROVIDER", "ollama"),
            ("OLLAMA_ENDPOINT", "http://127.0.0.1:12345/v1/"), ("OLLAMA_MODEL", "other-local"),
            ("OLLAMA_ATTACK_MODEL", "other-attack"), ("OLLAMA_JUDGE_MODEL", "other-judge"));
        Assert.Equal("http://127.0.0.1:12345/v1/", settings.Endpoint.AbsoluteUri);
        Assert.Equal("other-local", settings.Model);
        Assert.Equal("other-attack", settings.GetModel("AttackModel"));
        Assert.Equal("other-judge", settings.GetModel("JudgeModel"));
    }

    [Fact]
    public void EnvironmentCanSwitchBackToFoundry()
    {
        var settings = Load(Profiles.Replace("\"AI\": {", "\"AI\": { \"Provider\": \"ollama\","),
            ("AI_PROVIDER", "foundry"), ("AI_MODEL", "override-agent"),
            ("AI_API_KEY", "override-key"), ("AI_ENDPOINT", "https://override.example/v1/"));
        Assert.Equal("foundry", settings.Provider);
        Assert.Equal("override-agent", settings.Model);
        Assert.Equal("override-key", settings.ApiKey);
        Assert.Equal("https://override.example/v1/", settings.Endpoint.AbsoluteUri);
    }

    [Fact]
    public void OllamaCanStartWithoutASettingsFile()
    {
        var settings = Load(null, ("AI_PROVIDER", "ollama"));
        Assert.Equal("qwen3:8b", settings.Model);
        Assert.Equal("ollama", settings.ApiKey);
    }

    [Theory]
    [InlineData("http://remote.example/v1/")]
    [InlineData("ftp://localhost/v1/")]
    [InlineData("not a url")]
    public void InvalidEndpointFailsBeforeAnyRequest(string endpoint) =>
        Assert.Throws<ArgumentException>(() => Load(null,
            ("AI_PROVIDER", "ollama"), ("OLLAMA_ENDPOINT", endpoint)));

    [Fact]
    public void UnknownProviderFailsInsteadOfSilentlyCallingCloud() =>
        Assert.Throws<InvalidOperationException>(() => Load(Profiles, ("AI_PROVIDER", "olama")));

    [Fact]
    public void FoundryStillRequiresCredentials()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Load("""
            { "AI": { "Endpoint": "https://foundry.example/v1/", "Model": "cloud-agent" } }
            """));
        Assert.Contains("AI_API_KEY", error.Message);
    }

    public void Dispose() => File.Delete(path);
}
