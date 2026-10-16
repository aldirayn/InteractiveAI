using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
public class SessionsController : ControllerBase
{
    private readonly IChatService _chat;
    public SessionsController(IChatService chat) => _chat = chat;

    [HttpGet("api/sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
        => Ok(await _chat.GetSessionsAsync(ct));

    [HttpGet("api/history/{sessionId:guid}")]
    public async Task<IActionResult> GetHistory(Guid sessionId, CancellationToken ct)
    {
        try { return Ok(await _chat.GetHistoryAsync(sessionId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("api/history/{sessionId:guid}")]
    public async Task<IActionResult> Delete(Guid sessionId, CancellationToken ct)
    {
        try { await _chat.DeleteSessionAsync(sessionId, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", time = DateTime.UtcNow });
}
