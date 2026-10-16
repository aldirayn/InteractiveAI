using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/voice")]
public class VoiceController : ControllerBase
{
    private readonly IVoiceService _voice;
    private readonly VoiceOptions _opt;
    public VoiceController(IVoiceService voice, IOptions<VoiceOptions> opt)
    {
        _voice = voice;
        _opt = opt.Value;
    }

    [HttpGet("voices")]
    public async Task<IActionResult> Voices()
        => Ok(new { data = await _voice.IndonesianVoicesAsync() });


    [HttpGet("tts")]
    public async Task<IActionResult> Tts([FromQuery] string text, [FromQuery] string? voice, [FromQuery] int? rate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Query 'text' wajib diisi." });
        try
        {
            var audio = await _voice.SpeakAsync(text, voice, rate, ct);

            var isMp3 = _voice is EdgeTtsService or ElevenLabsTtsService
                || (_voice is FallbackVoiceService && FallbackVoiceService.LastFormat == "mp3");
            // Header transparansi: voice id yang BENAR-BENAR berbunyi (bisa beda dari
            // yang diminta bila terjadi fallback DefaultVoiceId). Lihat di DevTools.
            var usedVoice = voice ?? _opt.DefaultVoice;
            if (_voice is ElevenLabsTtsService
                || (_voice is FallbackVoiceService && FallbackVoiceService.LastFormat == "mp3"))
                usedVoice = string.IsNullOrWhiteSpace(ElevenLabsTtsService.LastVoiceId)
                    ? usedVoice : ElevenLabsTtsService.LastVoiceId;
            else if (_voice is FallbackVoiceService)
                usedVoice = "mms-fallback-lokal";
            Response.Headers["X-Voice-Id"] = usedVoice;
            return File(audio, isMp3 ? "audio/mpeg" : "audio/wav", isMp3 ? "tts.mp3" : "tts.wav");
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(502, new { error = ex.Message }); }
    }


    [HttpGet("elevenlabs")]
    public async Task<IActionResult> ElevenLabsVoices(CancellationToken ct)
    {
        ElevenLabsTtsService? eleven = _voice as ElevenLabsTtsService;
        if (eleven is null && _voice is FallbackVoiceService fb)
            eleven = fb.Primary;
        if (eleven is null)
            return BadRequest(new { error = "Provider suara aktif bukan ElevenLabs. Set Voice:Provider=ElevenLabs." });
        try { return Ok(new { data = await eleven.ListVoicesAsync(ct) }); }
        catch (InvalidOperationException ex) { return StatusCode(502, new { error = ex.Message }); }
    }


    [HttpGet("status")]
    public IActionResult Status()
    {
        if (_voice is FallbackVoiceService fb)
        {
            var tripped = FallbackVoiceService.QuotaTripped;
            return Ok(new
            {
                primary = "ElevenLabs",
                voices = _opt.ElevenLabs.Voices,
                defaultVoiceId = _opt.ElevenLabs.DefaultVoiceId,
                fallback = fb.FallbackName,
                fallbackEnabled = fb.FallbackEnabled,

                quotaTripped = tripped,
                quotaReason = tripped ? FallbackVoiceService.QuotaReason : "",
                retryPrimaryAfter = tripped ? FallbackVoiceService.QuotaDeadUntil : (DateTime?)null,
                browserVoice = _opt.BrowserVoice,
                note = $"Otomatis pindah ke {fb.FallbackName} bila ElevenLabs gagal (kredit habis 402 / 401 / network)."
            });
        }
        return Ok(new
        {
            primary = _voice.GetType().Name,
            fallback = "nonaktif",
            fallbackEnabled = false,
            browserVoice = _opt.BrowserVoice
        });
    }
}
