using System.Text.Json;
using InteractiveAI.Frontend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Frontend.Controllers;

public class ChatController : Controller
{
    private readonly BackendClient _backend;
    private readonly IWebHostEnvironment _env;
    public ChatController(BackendClient backend, IWebHostEnvironment env)
    {
        _backend = backend;
        _env = env;
    }

    public IActionResult Index() => View();

    [HttpGet("/Chat/Version")]
    public IActionResult Version()
    {
        var tick = 0L;
        try
        {
            foreach (var f in new[] { "chat.js", "voice-ui.js" })
            {
                var p = System.IO.Path.Combine(_env.WebRootPath, "js", f);
                if (System.IO.File.Exists(p))
                    tick = System.Math.Max(tick, System.IO.File.GetLastWriteTimeUtc(p).Ticks);
            }
        }
        catch { }
        return Json(new { jsHash = tick.ToString(), app = GetType().Assembly.GetName().Version?.ToString() ?? "" });
    }

    [HttpGet("/Chat/Models")]
    public async Task<IActionResult> Models(CancellationToken ct)
    {
        try { return Json(new { data = await _backend.GetModelsAsync(ct) }); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpPost("/Chat/Send")]
    public async Task<IActionResult> Send([FromBody] SendChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message kosong." });
        try { return Content((await _backend.SendChatAsync(req, ct)).GetRawText(), "application/json"); }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpPost("/Chat/Greet")]
    public async Task<IActionResult> Greet([FromBody] GreetChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Greeting))
            return BadRequest(new { error = "Greeting kosong." });
        try { return Content((await _backend.GreetAsync(req, ct)).GetRawText(), "application/json"); }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    public record WoFromSessionRequest(Guid SessionId);

    [HttpPost("/Chat/WO")]
    public async Task<IActionResult> CreateWo([FromBody] WoFromSessionRequest req, CancellationToken ct)
    {
        if (req.SessionId == Guid.Empty)
            return BadRequest(new { error = "Belum ada percakapan. Sapa sopir dulu." });
        try { return Content((await _backend.CreateWoAsync(req.SessionId, ct)).GetRawText(), "application/json"); }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/WO")]
    public async Task<IActionResult> WoList(CancellationToken ct)
    {
        try { return Content(await _backend.GetWoListRawAsync(ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }


    [HttpGet("/Chat/QrLookup")]
    public async Task<IActionResult> QrLookup([FromQuery] string srd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(srd))
            return BadRequest(new { error = "SRD kosong." });
        try { return Content(await _backend.QrLookupAsync(srd, ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/KimoExists")]
    public async Task<IActionResult> KimoExists([FromQuery] string srd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(srd))
            return BadRequest(new { error = "SRD kosong." });
        try { return Content(await _backend.KimoExistsAsync(srd, ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }


    [HttpPost("/Chat/StreamSend")]
    public async Task StreamSend([FromBody] SendChatRequest req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "Message kosong." }, ct);
            return;
        }
        HttpResponseMessage backendRes;
        try
        {
            backendRes = await _backend.StreamChatAsync(req, ct);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 502;
            await Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
            return;
        }
        using (backendRes)
        {
            if (!backendRes.IsSuccessStatusCode)
            {
                var body = await backendRes.Content.ReadAsStringAsync(ct);
                Response.ContentType = "text/event-stream";
                await Response.WriteAsync(
                    "data: " + JsonSerializer.Serialize(new { type = "error", error = body }) + "\n\n", ct);
                return;
            }
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            await using var s = await backendRes.Content.ReadAsStreamAsync(ct);
            await s.CopyToAsync(Response.Body, ct);
        }
    }
    public async Task<IActionResult> MasterLookup([FromQuery] string nopol, [FromQuery] string? driver,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nopol))
            return BadRequest(new { error = "Nopol kosong." });
        try { return Content(await _backend.LookupMasterAsync(nopol, driver, ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/Tts")]
    public async Task<IActionResult> Tts([FromQuery] string text, [FromQuery] string? voice, [FromQuery] int? rate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { error = "Teks kosong." });
        try
        {
            var (bytes, type, voiceId) = await _backend.GetTtsAsync(text, voice, rate, ct);
            if (!string.IsNullOrWhiteSpace(voiceId))
                Response.Headers["X-Voice-Id"] = voiceId;
            return File(bytes, type);
        }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }


    [HttpPost("/Chat/Stt")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Stt(IFormFile audio, CancellationToken ct)
    {
        if (audio is null || audio.Length == 0) return BadRequest(new { error = "Audio kosong." });
        try { return Content(await _backend.SttTranscribeAsync(audio, ct), "application/json"); }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/SttStatus")]
    public async Task<IActionResult> SttStatus(CancellationToken ct)
    {
        try { return Content(await _backend.GetSttStatusRawAsync(ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/VoiceStatus")]
    public async Task<IActionResult> VoiceStatus(CancellationToken ct)
    {
        try { return Content(await _backend.GetVoiceStatusRawAsync(ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/Sessions")]
    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        try { return Content(await _backend.GetSessionsRawAsync(ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpGet("/Chat/History/{id:guid}")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        try { return Content(await _backend.GetHistoryRawAsync(id, ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpDelete("/Chat/Sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken ct)
    {
        try { await _backend.DeleteSessionAsync(id, ct); return NoContent(); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }
}
