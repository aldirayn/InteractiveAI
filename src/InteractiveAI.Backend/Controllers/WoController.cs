using InteractiveAI.Backend.DTOs;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/wo")]
public class WoController : ControllerBase
{
    private readonly IWoService _wo;
    private readonly ILogger<WoController> _log;
    public WoController(IWoService wo, ILogger<WoController> log) { _wo = wo; _log = log; }

    public record FromSessionRequest(Guid SessionId);


    [HttpPost("from-session")]
    public async Task<IActionResult> FromSession([FromBody] FromSessionRequest req, CancellationToken ct)
    {
        try { return Ok(await _wo.CreateFromSessionAsync(req.SessionId, ct)); }
        catch (KeyNotFoundException ex)
        {
            _log.LogWarning(ex, "WO sesi {SessionId} gagal: sesi tidak ditemukan", req.SessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _log.LogWarning(ex, "WO sesi {SessionId} gagal: {Error}", req.SessionId, ex.Message);
            return StatusCode(502, new { error = ex.Message });
        }
    }
      
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _wo.ListAsync(ct));

    [HttpGet("kimo-exists")]
    public async Task<IActionResult> KimoExists([FromQuery] string srd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(srd)) return BadRequest(new { error = "SRD kosong." });
        var exists = await _wo.KimoExistsAsync(srd, ct);
        return Ok(new { exists, wos = exists ? await _wo.KimoWoListAsync(srd, ct) : new() });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        try { return Ok(await _wo.GetAsync(id, ct)); }
        catch (KeyNotFoundException ex)
        {
            _log.LogWarning(ex, "WO id {Id} tidak ditemukan", id);
            return NotFound(new { error = ex.Message });
        }
    }
}
