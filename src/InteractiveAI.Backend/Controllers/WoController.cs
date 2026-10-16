using InteractiveAI.Backend.DTOs;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/wo")]
public class WoController : ControllerBase
{
    private readonly IWoService _wo;
    public WoController(IWoService wo) => _wo = wo;

    public record FromSessionRequest(Guid SessionId);


    [HttpPost("from-session")]
    public async Task<IActionResult> FromSession([FromBody] FromSessionRequest req, CancellationToken ct)
    {
        try { return Ok(await _wo.CreateFromSessionAsync(req.SessionId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(502, new { error = ex.Message }); }
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
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
