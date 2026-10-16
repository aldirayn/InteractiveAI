using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/stt")]
public class SttController : ControllerBase
{
    private const int SampleRate = 16000;
    private const int MaxSeconds = 30;
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly HushDenoiseService _hush;
    private readonly WhisperSttService _whisper;
    private readonly SttOptions _opt;

    public SttController(HushDenoiseService hush, WhisperSttService whisper, IOptions<SttOptions> opt)
    {
        _hush = hush;
        _whisper = whisper;
        _opt = opt.Value;
    }

    public record SttResult(string Text, string Engine, bool Denoised);

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Transcribe(IFormFile audio, [FromForm] string? denoise, CancellationToken ct)
    {
        if (!_opt.Whisper.Enabled)
            return StatusCode(503, new { error = "STT backend dimatikan." });
        if (audio is null || audio.Length == 0)
            return BadRequest(new { error = "Field 'audio' (wav) wajib diisi." });
        float[] pcm;
        try
        {
            pcm = await ReadWav16kMono(audio, ct);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        if (pcm.Length > SampleRate * MaxSeconds)
            return BadRequest(new { error = $"Audio maksimal {MaxSeconds} detik." });

        var denoised = false;
        var hushOn = _opt.Hush.Enabled
            && !string.Equals(denoise, "0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(denoise, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(denoise, "off", StringComparison.OrdinalIgnoreCase);
        if (hushOn)
        {
            try
            {
                var clean = _hush.Denoise(pcm);

                denoised = !ReferenceEquals(clean, pcm);
                pcm = clean;
            }
            catch (Exception ex)
            {

                HttpContext.Items["stt-warn"] = ex.Message;
            }
        }

        string text;
        try
        {
            text = await _whisper.TranscribeAsync(pcm, ct);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
        var engine = denoised ? "hush+whisper" : "whisper";
        return Ok(new SttResult(text, engine, denoised));
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var provider = string.Equals(_opt.Provider, "Backend", StringComparison.OrdinalIgnoreCase)
            ? "Backend" : "Browser";
        return Ok(new
        {
            provider,
            hushEnabled = _opt.Hush.Enabled,
            whisperEnabled = _opt.Whisper.Enabled,
            whisperModel = _opt.Whisper.ModelPath,
            note = "Model dimuat lazy saat request pertama (lihat log bila fallback)."
        });
    }

    private static async Task<float[]> ReadWav16kMono(IFormFile audio, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms, ct);
        var b = ms.ToArray();
        if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F')
            throw new ArgumentException("Bukan file WAV valid (kirim wav PCM16 mono 16kHz).");
        int sr = BitConverter.ToInt32(b, 24);
        short ch = BitConverter.ToInt16(b, 22);
        short bits = BitConverter.ToInt16(b, 34);
        if (bits != 16 || ch != 1 || sr != SampleRate)
            throw new ArgumentException($"WAV harus PCM16 mono {SampleRate}Hz (dapat {bits}bit/{ch}ch/{sr}Hz).");
        int n = (b.Length - 44) / 2;
        var pcm = new float[n];
        for (int i = 0; i < n; i++)
            pcm[i] = BitConverter.ToInt16(b, 44 + i * 2) / 32768f;
        return pcm;
    }
}
