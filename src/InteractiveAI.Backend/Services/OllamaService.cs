using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.DTOs;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;

public interface IOllamaService
{
    Task<List<string>> GetModelsAsync(CancellationToken ct = default);
    Task<string> ChatAsync(string model, List<OllamaChatMessage> messages, double temperature, CancellationToken ct = default);

    IAsyncEnumerable<string> ChatStreamAsync(string model, List<OllamaChatMessage> messages, double temperature, CancellationToken ct = default);
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opt;
    private readonly ILogger<OllamaService> _log;

    public OllamaService(HttpClient http, IOptions<OllamaOptions> opt, ILogger<OllamaService> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;

        var key = string.IsNullOrWhiteSpace(_opt.ApiKey)
            ? Environment.GetEnvironmentVariable("OLLAMA_API_KEY")
            : _opt.ApiKey;
        if (!string.IsNullOrWhiteSpace(key) && _http.DefaultRequestHeaders.Authorization is null)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken ct = default)
    {

        var res = await _http.GetAsync("/api/tags", ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: ct);
        return body?.Models
            .Select(m => string.IsNullOrWhiteSpace(m.Name) ? m.Model : m.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x)
            .ToList() ?? new();
    }

    public async Task<string> ChatAsync(string model, List<OllamaChatMessage> messages, double temperature, CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = new OllamaChatOptions { Temperature = temperature }
        };

        var res = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Ollama error {Status}: {Err}", res.StatusCode, err);
            throw new HttpRequestException($"Ollama returned {(int)res.StatusCode}: {err}");
        }
        var body = await res.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
        var content = body?.Message.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Ollama returned empty response.");
        return content;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string model, List<OllamaChatMessage> messages, double temperature, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = true,
            Options = new OllamaChatOptions { Temperature = temperature }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
        req.Content = JsonContent.Create(payload);
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Ollama stream error {Status}: {Err}", res.StatusCode, err);
            throw new HttpRequestException($"Ollama returned {(int)res.StatusCode}: {err}");
        }
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? delta = null;
            var done = false;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True)
                    done = true;
                if (root.TryGetProperty("message", out var m)
                    && m.TryGetProperty("content", out var c)
                    && c.ValueKind == JsonValueKind.String)
                    delta = c.GetString();
            }
            catch (JsonException) { continue; }
            if (!string.IsNullOrEmpty(delta)) yield return delta;
            if (done) break;
        }
    }
}
