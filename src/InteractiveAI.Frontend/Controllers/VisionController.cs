using InteractiveAI.Frontend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Frontend.Controllers;

public class VisionController : Controller
{
    private readonly BackendClient _backend;
    public VisionController(BackendClient backend) => _backend = backend;

    public IActionResult Index() => View();

    [HttpGet("/Vision/Info")]
    public async Task<IActionResult> Info(CancellationToken ct)
    {
        try { return Content(await _backend.GetVisionInfoRawAsync(ct), "application/json"); }
        catch (Exception ex) { return StatusCode(502, new { error = ex.Message }); }
    }

    [HttpPost("/Vision/Detect")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Detect(IFormFile image, [FromQuery] bool qr = true, CancellationToken ct = default)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "File gambar wajib diisi." });
        try
        {
            using var stream = image.OpenReadStream();
            return Content(await _backend.DetectAsync(stream, image.FileName, image.ContentType, qr, ct), "application/json");
        }
        catch (HttpRequestException ex) { return StatusCode(502, new { error = ex.Message }); }
    }
}
