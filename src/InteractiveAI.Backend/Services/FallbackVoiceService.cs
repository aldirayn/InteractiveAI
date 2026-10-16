using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;


public class FallbackVoiceService : IVoiceService
{
    private readonly ElevenLabsTtsService _primary;
    private readonly IVoiceService _fallback;
    private readonly VoiceOptions _opt;
    private readonly ILogger<FallbackVoiceService> _log;


    private static readonly AsyncLocal<string> _lastFormat = new();

    public static string LastFormat => _lastFormat.Value ?? "mp3";


    private static DateTime _quotaDeadUntil = DateTime.MinValue;
    private static string _quotaReason = "";
    private static readonly object _quotaLock = new();

    public static bool QuotaTripped
    {
        get { lock (_quotaLock) { return DateTime.UtcNow < _quotaDeadUntil; } }
    }

    public static string QuotaReason
    {
        get { lock (_quotaLock) { return _quotaReason; } }
    }

    public static DateTime QuotaDeadUntil
    {
        get { lock (_quotaLock) { return _quotaDeadUntil; } }
    }


    public FallbackVoiceService(
        ElevenLabsTtsService primary,
        IServiceProvider services,
        IOptions<VoiceOptions> opt,
        ILogger<FallbackVoiceService> log)
    {
        _primary = primary;
        _opt = opt.Value;
        _log = log;
        _fallback = _opt.Fallback.Provider?.Trim().ToLowerInvariant() switch
        {
            "vits" => services.GetRequiredService<VitsTtsService>(),
            _ => services.GetRequiredService<MmsTtsService>()
        };
    }


    public string FallbackName => _fallback is VitsTtsService ? "Vits (lokal, gadis/ardi)" : "Mms (lokal, gratis)";

    public bool FallbackEnabled => _opt.Fallback.Enabled;


    public ElevenLabsTtsService Primary => _primary;

    public async Task<byte[]> SpeakAsync(string text, string? voice, int? rate, CancellationToken ct)
    {

        if (!FallbackEnabled)
        {
            var audio = await _primary.SpeakAsync(text, voice, rate, ct);
            _lastFormat.Value = "mp3";
            return audio;
        }


        if (QuotaTripped)
        {
            _log.LogDebug("Kuota ElevenLabs habis ({Reason}), langsung MMS lokal.", QuotaReason);
            var cached = await _fallback.SpeakAsync(text, voice, rate, ct);
            _lastFormat.Value = "wav";
            return cached;
        }

        try
        {
            var audio = await _primary.SpeakAsync(text, voice, rate, ct);
            _lastFormat.Value = "mp3";

            lock (_quotaLock) { _quotaDeadUntil = DateTime.MinValue; _quotaReason = ""; }
            return audio;
        }
        catch (ArgumentException)
        {

            throw;
        }
        catch (InvalidOperationException ex) when (IsQuotaError(ex))
        {

            TripQuotaBreaker(ex.Message);
            _log.LogWarning(ex, "ElevenLabs gagal (kuota/plan/auth) — fallback ke {Fallback} + cooldown {Min} menit.",
                _opt.Fallback.Provider, CooldownMinutes());
            var audio = await _fallback.SpeakAsync(text, voice, rate, ct);
            _lastFormat.Value = "wav";
            return audio;
        }
        catch (Exception ex)
        {

            _log.LogWarning(ex, "TTS utama (ElevenLabs) gagal, fallback ke {Fallback}.", _opt.Fallback.Provider);
            var audio = await _fallback.SpeakAsync(text, voice, rate, ct);
            _lastFormat.Value = "wav";
            return audio;
        }
    }


    private static bool IsQuotaError(InvalidOperationException ex)
        => ex.Message.Contains("402") || ex.Message.Contains("401") || ex.Message.Contains("429")
            || ex.Message.Contains("Kuota", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("kredit", StringComparison.OrdinalIgnoreCase);

    private void TripQuotaBreaker(string reason)
    {
        lock (_quotaLock)
        {
            _quotaDeadUntil = DateTime.UtcNow.AddMinutes(CooldownMinutes());
            _quotaReason = reason.Length > 120 ? reason[..120] : reason;
        }
    }

    private int CooldownMinutes() => Math.Clamp(_opt.Fallback.QuotaCooldownMinutes, 1, 24 * 60);

    public async Task<List<string>> IndonesianVoicesAsync()
        => await _primary.IndonesianVoicesAsync();
}
