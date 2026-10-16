using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/models")]
public class ModelsController : ControllerBase
{
    private readonly IOllamaService _ollama;
    public ModelsController(IOllamaService ollama) => _ollama = ollama;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var models = await _ollama.GetModelsAsync(ct);
            return Ok(new { data = models });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = $"Gagal mengambil model dari Ollama: {ex.Message}" });
        }
    }
}
