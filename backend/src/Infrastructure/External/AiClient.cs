using System.Net.Http.Json;
using Application.Abstractions;
namespace Infrastructure.External;

/// Calls the separate AI Service. If it is down/slow, falls back to simple rules (IsFallback = true).
/// Polly retry + circuit breaker are attached in Api/Program.cs.
public class AiClient : IAiClient
{
    private readonly HttpClient _http;
    public AiClient(HttpClient http) => _http = http;

    public async Task<AiResult> ClassifyAsync(string description, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/classify", new { description }, ct);
            res.EnsureSuccessStatusCode();
            var dto = await res.Content.ReadFromJsonAsync<AiDto>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Empty AI response");
            return new AiResult(dto.Category, dto.Urgency, dto.Confidence, dto.Rationale, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                   or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Fallback(description);
        }
    }

    private static AiResult Fallback(string text)
    {
        var t = text.ToLowerInvariant();
        string cat = t.Contains("medic") || t.Contains("insulin") ? "Medicine"
                   : t.Contains("water") ? "Water" : t.Contains("food") || t.Contains("hungry") ? "Food"
                   : t.Contains("shelter") || t.Contains("roof") ? "Shelter" : "Other";
        int urgency = t.Contains("injur") || t.Contains("urgent") || t.Contains("trapped") ? 5 : 3;
        return new AiResult(cat, urgency, 0.30m, "Rule-based fallback: AI service unavailable.", true);
    }

    private record AiDto(string Category, int Urgency, decimal Confidence, string Rationale);
}
