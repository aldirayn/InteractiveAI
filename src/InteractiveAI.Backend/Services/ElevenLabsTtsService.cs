using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;


public class ElevenLabsTtsService : IVoiceService
{
    // Voice id yang TERAKHIR berhasil dipakai (bisa beda dari yang diminta bila
    // terjadi fallback DefaultVoiceId). Best-effort diagnostik (bukan sinkron).
    public static string LastVoiceId { get; private set; } = "";

    private readonly VoiceOptions _opt;
    private readonly ElevenLabsOptions _el;
    private readonly IHttpClientFactory _factory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ElevenLabsTtsService> _log;

    public ElevenLabsTtsService(IOptions<VoiceOptions> opt, IHttpClientFactory factory,
        IMemoryCache cache, ILogger<ElevenLabsTtsService> log)
    {
        _opt = opt.Value;
        _el = _opt.ElevenLabs;
        _factory = factory;
        _cache = cache;
        _log = log;
    }

    public async Task<byte[]> SpeakAsync(string text, string? voice, int? rate, CancellationToken ct)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("Teks kosong.");
        if (text.Length > _opt.MaxChars)
            throw new ArgumentException($"Teks maksimal {_opt.MaxChars} karakter.");

        var apiKey = RequireApiKey();
        var voiceId = ResolveVoiceId(voice);

        // Baseline 0.75 (lambat-tenang untuk loket): Santai(0) -> 0.75,
        // Lambat(-10) -> 0.70 (batas bawah ElevenLabs), Cerah(+12) -> 0.87,
        // Cepat(+25) -> 1.00.
        var speed = _el.MapRateToSpeed
            ? Math.Clamp(0.75 + (rate ?? _opt.DefaultRate) / 100.0, 0.7, 1.2)
            : 0.75;

        var spoken = EdgeTtsService.NormalizeForSpeech(text);

        var cacheKey = $"el-tts:{voiceId}|{speed:0.00}|{Hash(spoken)}";
        if (_cache.TryGetValue(cacheKey, out byte[]? hit) && hit is { Length: > 0 })
        {
            _log.LogDebug("TTS ElevenLabs cache hit {Voice} speed {Speed} ({Chars} char)", voiceId, speed, spoken.Length);
            return hit;
        }

        var attempt = await RequestAsync(apiKey, voiceId, speed, spoken, ct);


        var altId = _el.DefaultVoiceId?.Trim();
        if (!attempt.Ok && attempt.Status == 402
            && !string.IsNullOrWhiteSpace(altId)
            && !altId.Equals(voiceId, StringComparison.OrdinalIgnoreCase)
            && attempt.Detail.Contains("paid_plan_required", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("Voice {Voice} ditolak 402 (library voice di plan Free) — coba DefaultVoiceId {Alt}.",
                voiceId, altId);
            var alt = await RequestAsync(apiKey, altId, speed, spoken, ct);
            if (alt.Ok)
            {
                voiceId = altId;
                cacheKey = $"el-tts:{voiceId}|{speed:0.00}|{Hash(spoken)}";
                attempt = alt;
            }
        }

        if (!attempt.Ok)
        {
            _log.LogWarning("ElevenLabs TTS gagal {Status} voice {Voice}: {Detail}",
                attempt.Status, voiceId, attempt.Detail);
            throw new InvalidOperationException(
                $"ElevenLabs TTS gagal ({attempt.Status}): {FriendlyError((System.Net.HttpStatusCode)attempt.Status, attempt.Detail)}{attempt.Detail}");
        }

        _cache.Set(cacheKey, attempt.Audio!, TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 1, 24 * 60)));
        LastVoiceId = voiceId;
        return attempt.Audio!;
    }


    private readonly record struct Attempt(bool Ok, int Status, string Detail, byte[]? Audio);


    private async Task<Attempt> RequestAsync(string apiKey, string voiceId, double speed, string spoken, CancellationToken ct)
    {
        var url = $"{_el.BaseUrl.TrimEnd('/')}/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}" +
                  $"?output_format={Uri.EscapeDataString(_el.OutputFormat)}";
        var settings = new Dictionary<string, object?>
        {
            ["stability"] = _el.Stability,
            ["similarity_boost"] = _el.SimilarityBoost,
            ["style"] = _el.Style,
            ["use_speaker_boost"] = _el.SpeakerBoost,
            ["speed"] = speed
        };
        var payload = new Dictionary<string, object?>
        {
            ["text"] = spoken,
            ["model_id"] = _el.ModelId,
            ["voice_settings"] = settings
        };

        using var http = _factory.CreateClient(nameof(ElevenLabsTtsService));
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_el.TimeoutSeconds, 5, 300));
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        req.Headers.Add("xi-api-key", apiKey);
        req.Headers.Accept.ParseAdd("audio/mpeg");

        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return new Attempt(false, (int)res.StatusCode, Trim(await res.Content.ReadAsStringAsync(ct)), null);

        var mp3 = await res.Content.ReadAsByteArrayAsync(ct);
        if (mp3.Length == 0)
            return new Attempt(false, 200, "ElevenLabs mengembalikan audio kosong.", null);
        return new Attempt(true, (int)res.StatusCode, "", mp3);
    }


    public Task<List<string>> IndonesianVoicesAsync()
    {
        var mapped = _el.Voices
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .OrderBy(x => x)
            .ToList();
        if (mapped.Count == 0) mapped.Add(_opt.DefaultVoice);
        return Task.FromResult(mapped);
    }


    public async Task<List<object>> ListVoicesAsync(CancellationToken ct)
    {
        var apiKey = RequireApiKey();
        using var http = _factory.CreateClient(nameof(ElevenLabsTtsService));
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_el.TimeoutSeconds, 5, 300));
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_el.BaseUrl.TrimEnd('/')}/v1/voices");
        req.Headers.Add("xi-api-key", apiKey);

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"ElevenLabs daftar voice gagal ({(int)res.StatusCode}): {FriendlyError(res.StatusCode)}{Trim(body)}");

        var list = new List<object>();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("voices", out var arr))
        {
            foreach (var v in arr.EnumerateArray())
            {
                list.Add(new
                {
                    voiceId = Str(v, "voice_id"),
                    name = Str(v, "name"),
                    category = Str(v, "category"),
                    labels = v.TryGetProperty("labels", out var l) ? l.Clone() : (JsonElement?)null
                });
            }
        }
        return list;
    }

    private string RequireApiKey()
    {
        var key = string.IsNullOrWhiteSpace(_el.ApiKey)
            ? Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY")
            : _el.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "API key ElevenLabs belum diisi. Isi Voice:ElevenLabs:ApiKey di appsettings.json " +
                "(atau set env var ELEVENLABS_API_KEY), lalu jalankan ulang backend.");
        return key.Trim();
    }


    private string ResolveVoiceId(string? voice)
    {
        // Kosong = default eksplisit (jalur normal: dropdown / DefaultVoice).
        if (string.IsNullOrWhiteSpace(voice))
        {
            var def = _opt.DefaultVoice;
            if (_el.Voices.TryGetValue(def, out var did) && !string.IsNullOrWhiteSpace(did)) return did.Trim();
            if (!string.IsNullOrWhiteSpace(_el.DefaultVoiceId)) return _el.DefaultVoiceId.Trim();
            throw new ArgumentException(
                $"Voice default '{def}' belum dipetakan. Isi Voice:ElevenLabs:Voices['{def}'] " +
                "atau Voice:ElevenLabs:DefaultVoiceId di appsettings.json.");
        }
        var name = voice.Trim();
        if (_el.Voices.TryGetValue(name, out var id) && !string.IsNullOrWhiteSpace(id)) return id.Trim();
        // ID mentah langsung (kompatibilitas pemanggil API langsung).
        if (name.Length >= 16 && name.All(char.IsLetterOrDigit)) return name;
        // NAMA ASING = 400 KERAS, bukan fallback diam-diam ke DefaultVoiceId.
        // (Pernah: 'browser' lolos jadi Sarah tanpa suara. Fail loud > salah suara.)
        throw new ArgumentException(
            $"Voice '{name}' tidak dikenal. Isi Voice:ElevenLabs:Voices['{name}'] " +
            "atau Voice:ElevenLabs:DefaultVoiceId di appsettings.json.");
    }


    private static string FriendlyError(System.Net.HttpStatusCode status, string detail = "")
    {
        if (status == System.Net.HttpStatusCode.PaymentRequired)
        {
            if (detail.Contains("paid_plan_required", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("library voice", StringComparison.OrdinalIgnoreCase))
                return "Voice ini butuh plan berbayar (library/community voice tidak boleh dipakai lewat API di plan Free — bukan kredit habis). ";
            return "Kuota/kredit ElevenLabs habis. ";
        }
        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized => "API key ElevenLabs tidak valid/ditolak. ",
            System.Net.HttpStatusCode.NotFound => "voice id atau model tidak ditemukan. ",
            System.Net.HttpStatusCode.UnprocessableEntity => "teks/parameter tidak diterima model (bila memakai speed, coba set Voice:ElevenLabs:MapRateToSpeed=false). ",
            System.Net.HttpStatusCode.TooManyRequests => "terlalu banyak permintaan (rate limit). ",
            _ => ""
        };
    }

    private static string? Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Trim(string s)
        => string.IsNullOrWhiteSpace(s) ? "" : (s.Length > 300 ? s[..300] + "..." : s);

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..16];
    }
}
