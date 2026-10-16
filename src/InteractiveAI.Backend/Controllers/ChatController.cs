using System.Text;
using System.Text.Json;
using InteractiveAI.Backend.DTOs;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    public ChatController(IChatService chat) => _chat = chat;

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _chat.SendAsync(req, ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    public record GreetRequest(string? Model, string? SystemPrompt, string Greeting);


    [HttpPost("greet")]
    public async Task<ActionResult<ChatResponse>> Greet([FromBody] GreetRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Greeting))
            return BadRequest(new { error = "Greeting kosong." });
        return Ok(await _chat.GreetAsync(req.Model, req.SystemPrompt, req.Greeting, ct));
    }


    [HttpPost("stream")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "Message kosong." }, ct);
            return;
        }
        ChatStreamStart start;
        try
        {
            start = await _chat.StartStreamAsync(req, ct);
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
            return;
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = 404;
            await Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var full = new StringBuilder();
        var gotToken = false;
        try
        {
            await foreach (var tok in _chat.StreamTokensAsync(start, req.Temperature, ct))
            {
                gotToken = true;
                full.Append(tok);
                await Response.WriteAsync(
                    "data: " + JsonSerializer.Serialize(new { type = "token", text = tok }) + "\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            var done = await _chat.FinishStreamAsync(start.SessionId, full.ToString(), ct);
            await Response.WriteAsync(
                "data: " + JsonSerializer.Serialize(new { type = "done", sessionId = done.SessionId, model = done.Model }) + "\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {

            if (gotToken)
            {
                try { await _chat.FinishStreamAsync(start.SessionId, full.ToString(), CancellationToken.None); }
                catch {  }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            if (gotToken)
            {
                try { await _chat.FinishStreamAsync(start.SessionId, full.ToString(), CancellationToken.None); }
                catch {  }
            }
            await Response.WriteAsync(
                "data: " + JsonSerializer.Serialize(new { type = "error", error = ex.Message }) + "\n\n",
                CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }
}
