using System.Net.Http.Json;
using System.Text.Json;
using BabyLog.Contracts;
using BabyLog.Domain;

namespace BabyLog.Infrastructure;

public sealed class HttpBabyLogGateway(HttpClient client) : IBabyLogGateway
{
    public Task<BabyProfile> GetBabyAsync(CancellationToken cancellationToken = default) => GetAsync<BabyProfile>("api/baby", cancellationToken);
    public Task<ActivityEntry[]> GetActivitiesAsync(DateOnly date, CancellationToken cancellationToken = default) => GetAsync<ActivityEntry[]>($"api/activities?date={date:yyyy-MM-dd}", cancellationToken);
    public Task<DailySummary> GetSummaryAsync(DateOnly date, CancellationToken cancellationToken = default) => GetAsync<DailySummary>($"api/summary?date={date:yyyy-MM-dd}", cancellationToken);
    public async Task<ActivityEntry> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await client.PostAsJsonAsync("api/activities", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ActivityEntry>(cancellationToken) ?? throw new InvalidOperationException("Il server non ha restituito la registrazione.");
    }
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await client.DeleteAsync($"api/activities/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }
    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidOperationException("Risposta del server vuota. Riprova tra un momento.");
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var message = $"Non è stato possibile completare la richiesta (HTTP {(int)response.StatusCode}).";
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) message = detail.GetString() ?? message;
            else if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String) message = title.GetString() ?? message;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray()).Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s));
                message = string.Join(" ", messages);
            }
        }
        catch (JsonException) { }
        throw new InvalidOperationException(message);
    }
}
