using System.Net.Http.Json;
using System.Text.Json;
using InteractiveAI.Frontend.Config;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Frontend.Services;

public record SendChatRequest(Guid? SessionId, string? Model, string Message, string? SystemPrompt);
public record GreetChatRequest(string? Model, string? SystemPrompt, string Greeting);
public record ChatSendResult(Guid SessionId, string Model, string Response);

public class BackendClient
{
    private readonly HttpClient _http;
    public BackendClient(HttpClient http, IOptions<BackendOptions> opt)
    {
        _http = http;
        _http.BaseAddress = new Uri(opt.Value.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(150);
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync("/api/models", ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("data", out var arr))
            return arr.EnumerateArray().Select(x => x.GetString()!).Where(x => x != null).ToList();
        return new();
    }

    public async Task<JsonElement> SendChatAsync(SendChatRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("/api/chat", new
        {
            sessionId = req.SessionId,
            model = req.Model,
            message = req.Message,
            temperature = 0.7,
            systemPrompt = req.SystemPrompt
        }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public async Task<JsonElement> GreetAsync(GreetChatRequest req, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("/api/chat/greet", new
        {
            model = req.Model,
            systemPrompt = req.SystemPrompt,
            greeting = req.Greeting
        }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public async Task<string> GetSessionsRawAsync(CancellationToken ct = default)
        => await _http.GetStringAsync("/api/sessions", ct);

    public async Task<string> GetHistoryRawAsync(Guid id, CancellationToken ct = default)
        => await _http.GetStringAsync($"/api/history/{id}", ct);

    public async Task<string> GetVisionInfoRawAsync(CancellationToken ct = default)
        => await _http.GetStringAsync("/api/vision/info", ct);

    public async Task<string> DetectAsync(Stream image, string fileName, string contentType, bool qr, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(image);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "image", fileName);
        var res = await _http.PostAsync($"/api/vision/detect?qr={qr.ToString().ToLower()}", form, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return body;
    }

    public async Task DeleteSessionAsync(Guid id, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"/api/history/{id}", ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> CreateWoAsync(Guid sessionId, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("/api/wo/from-session", new { sessionId }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public async Task<string> GetWoListRawAsync(CancellationToken ct = default)
        => await _http.GetStringAsync("/api/wo", ct);

    public async Task<string> QrLookupAsync(string srd, CancellationToken ct = default)
    {
        var res = await _http.GetAsync($"/api/qr/lookup?srd={Uri.EscapeDataString(srd)}", ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return body;
    }

    public async Task<string> KimoExistsAsync(string srd, CancellationToken ct = default)
    {
        var res = await _http.GetAsync($"/api/wo/kimo-exists?srd={Uri.EscapeDataString(srd)}", ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return body;
    }


    public async Task<HttpResponseMessage> StreamChatAsync(SendChatRequest req, CancellationToken ct = default)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        msg.Content = JsonContent.Create(new
        {
            sessionId = req.SessionId,
            model = req.Model,
            message = req.Message,
            temperature = 0.7,
            systemPrompt = req.SystemPrompt
        });
        return await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public async Task<string> LookupMasterAsync(string nopol, string? driver, CancellationToken ct = default)
    {
        var url = $"/api/master/lookup?nopol={Uri.EscapeDataString(nopol)}";
        if (!string.IsNullOrWhiteSpace(driver)) url += $"&driver={Uri.EscapeDataString(driver)}";
        var res = await _http.GetAsync(url, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return body;
    }

    public async Task<(byte[] Bytes, string ContentType, string VoiceId)> GetTtsAsync(string text, string? voice, int? rate, CancellationToken ct = default)
    {
        var url = $"/api/voice/tts?text={Uri.EscapeDataString(text)}";
        if (!string.IsNullOrWhiteSpace(voice)) url += $"&voice={Uri.EscapeDataString(voice)}";
        if (rate.HasValue) url += $"&rate={rate.Value}";
        var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(await res.Content.ReadAsStringAsync(ct));
        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
        var type = res.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
        res.Headers.TryGetValues("X-Voice-Id", out var ids);
        return (bytes, type, ids?.FirstOrDefault() ?? "");
    }

    public async Task<string> SttTranscribeAsync(Microsoft.AspNetCore.Http.IFormFile audio, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms, ct);
        ms.Position = 0;
        var file = new StreamContent(ms);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(file, "audio", "utterance.wav");
        var res = await _http.PostAsync("/api/stt", form, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(body);
        return body;
    }

    public async Task<string> GetSttStatusRawAsync(CancellationToken ct = default)
        => await _http.GetStringAsync("/api/stt/status", ct);

    public async Task<string> GetVoiceStatusRawAsync(CancellationToken ct = default)
        => await _http.GetStringAsync("/api/voice/status", ct);
}
