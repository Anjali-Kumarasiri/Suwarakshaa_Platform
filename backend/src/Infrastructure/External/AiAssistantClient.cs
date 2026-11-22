using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions;
using Microsoft.Extensions.Logging;
namespace Infrastructure.External;

/// Talks to the AI service for the citizen assistant, coordinator summaries and translation.
/// Every method has a safe fallback so a failing AI service never breaks the platform.
public class AiAssistantClient : IAiAssistantClient
{
    private readonly HttpClient _http; private readonly ILogger<AiAssistantClient> _log;
    public AiAssistantClient(HttpClient http, ILogger<AiAssistantClient> log) { _http = http; _log = log; }
    private record Answer(string Text);

    public async Task<AiText> AskAsync(string message, string language, CancellationToken ct = default)
    {
        try { return new AiText(await Post("/assist", new { message, language }, ct), false); }
        catch (Exception ex) when (IsTransient(ex))
        {
            _log.LogWarning(ex, "Assistant unavailable; using canned guidance.");
            return new AiText(LocalFaq.Answer(message, language), true);
        }
    }

    public async Task<AiText> SummarizeAsync(string statsJson, string language, CancellationToken ct = default)
    {
        try { return new AiText(await Post("/summarize", new { statsJson, language }, ct), false); }
        catch (Exception ex) when (IsTransient(ex))
        {
            _log.LogWarning(ex, "Summary service unavailable; building a plain summary.");
            var lines = new List<string> { "AI summary unavailable. Key figures:" };
            using var doc = JsonDocument.Parse(statsJson);
            foreach (var p in doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String))
                lines.Add($"- {p.Name}: {p.Value}");
            return new AiText(string.Join("\n", lines), true);
        }
    }

    public async Task<string?> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        try { return await Post("/translate", new { text, targetLanguage }, ct); }
        catch (Exception ex) when (IsTransient(ex)) { return null; }
    }

    private async Task<string> Post(string path, object body, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync(path, body, ct);
        res.EnsureSuccessStatusCode();
        var a = await res.Content.ReadFromJsonAsync<Answer>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        return string.IsNullOrWhiteSpace(a?.Text) ? throw new InvalidOperationException("Empty AI answer") : a!.Text;
    }
    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or Polly.CircuitBreaker.BrokenCircuitException;
}
