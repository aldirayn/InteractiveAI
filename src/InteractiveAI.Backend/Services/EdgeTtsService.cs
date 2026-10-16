using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EdgeTtsSharp;
using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;

public interface IVoiceService
{
    Task<byte[]> SpeakAsync(string text, string? voice, int? rate, CancellationToken ct);
    Task<List<string>> IndonesianVoicesAsync();
}

public class EdgeTtsService : IVoiceService
{
    private readonly VoiceOptions _opt;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EdgeTtsService> _log;

    public EdgeTtsService(IOptions<VoiceOptions> opt, IMemoryCache cache, ILogger<EdgeTtsService> log)
    {
        _opt = opt.Value;
        _cache = cache;
        _log = log;
    }

    public async Task<byte[]> SpeakAsync(string text, string? voice, int? rate, CancellationToken ct)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("Teks kosong.");
        if (text.Length > _opt.MaxChars)
            throw new ArgumentException($"Teks maksimal {_opt.MaxChars} karakter.");
        var name = string.IsNullOrWhiteSpace(voice) ? _opt.DefaultVoice : voice.Trim();

        var speed = Math.Clamp(rate ?? _opt.DefaultRate, -30, 50);


        var spoken = NormalizeForSpeech(text);

        var key = $"tts:{name}|{speed}|{Hash(spoken)}";
        if (_cache.TryGetValue(key, out byte[]? hit) && hit is { Length: > 0 })
        {
            _log.LogDebug("TTS cache hit {Voice} rate {Rate} ({Chars} char)", name, speed, spoken.Length);
            return hit;
        }

        EdgeTtsSharp.Structures.Voice v;
        try
        {
            v = await EdgeTts.GetVoice(name);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Voice '{name}' tidak dikenal. Contoh: id-ID-GadisNeural, id-ID-ArdiNeural.", ex);
        }

        try
        {
            var settings = new EdgeTtsSharp.Structures.PlaybackSettings { Rate = speed, Volume = 1.0f };
            using var stream = EdgeTts.GetAudioStream(v, spoken, settings, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            if (ms.Length == 0) throw new InvalidOperationException("Edge TTS mengembalikan audio kosong.");
            var mp3 = ms.ToArray();
            _cache.Set(key, mp3, TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 1, 24 * 60)));
            return mp3;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _log.LogWarning(ex, "Edge TTS gagal untuk voice {Voice}", name);
            throw new InvalidOperationException($"Gagal sintesis suara Edge TTS: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> IndonesianVoicesAsync()
    {

        try
        {
            var ids = new List<string>();
            foreach (var v in await EdgeTts.GetVoices())
            {
                var locale = v.Locale ?? string.Empty;
                var shortName = v.ShortName ?? string.Empty;
                if (locale.StartsWith("id", StringComparison.OrdinalIgnoreCase)
                    || shortName.StartsWith("id-ID", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(shortName) && !ids.Contains(shortName))
                        ids.Add(shortName);
                }
            }
            ids.Sort();
            if (ids.Count > 0) return ids;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Lookup voice offline gagal, pakai daftar bawaan.");
        }
        return new List<string> { "id-ID-GadisNeural", "id-ID-ArdiNeural" };
    }


    internal static string NormalizeForSpeech(string text)
    {
        var s = Regex.Replace(text, @"\s+", " ").Trim();

        s = Regex.Replace(s, @"\bWO-(\d{4})(\d{2})(\d{2})-(\d+)\b", m =>
            $"work order, tanggal {m.Groups[3].Value}, bulan {m.Groups[2].Value}, {m.Groups[1].Value}, nomor {Spaced(m.Groups[4].Value)}",
            RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bWO\b", "work order", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bQR\b", "Q R", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bKIMO\b", "Kimo", RegexOptions.IgnoreCase);

        s = Regex.Replace(s, @"\b([A-Z]{1,2})\s*(\d{1,4})\s*([A-Z]{1,3})\b", m =>
            $"{m.Groups[1].Value}, {Spaced(m.Groups[2].Value)}, {Spaced(m.Groups[3].Value)}");

        s = Regex.Replace(s, @"(?<![\d.])(\d{4})(?![\d.])", m => Spaced(m.Groups[1].Value));

        s = Regex.Replace(s, @"^(Halo|Hai|Selamat pagi|Selamat siang|Selamat sore|Selamat malam)(,|\s)", "$1, ");
        s = Regex.Replace(s, @"\b(ya|nah|jadi|oh iya)(,|\s)", "$1, ");

        if (!Regex.IsMatch(s, @"[.?!…]$")) s += ".";
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..16];
    }
}
