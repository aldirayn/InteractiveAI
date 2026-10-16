using InteractiveAI.Backend.DTOs;
using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/vision")]
public class VisionController : ControllerBase
{
    private readonly IVisionService _vision;
    public VisionController(IVisionService vision) => _vision = vision;

    [HttpGet("info")]
    public IActionResult Info() => Ok(new
    {
        model = _vision.Info.Model,
        inputSize = _vision.Info.InputSize,
        labels = _vision.Info.LabelCount,
        defaultClasses = _vision.Info.DefaultClasses,
        note = "YOLO11-COCO tidak punya kelas 'face'; deteksi wajah ≈ deteksi 'person'."
    });


    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        enabled = true,
        faceEnabled = false,
        model = _vision.Info.Model,
        inputSize = _vision.Info.InputSize,
        defaultClasses = _vision.Info.DefaultClasses
    });

    [HttpPost("detect")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Detect(
        IFormFile image,
        [FromQuery] float? confidence,
        [FromQuery] string? classes,
        [FromQuery] bool qr = true,
        CancellationToken ct = default)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "File gambar (field 'image') wajib diisi." });
        if (image.Length > 10_000_000)
            return BadRequest(new { error = "Gambar maksimal 10 MB." });

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await image.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        try
        {
            var cls = string.IsNullOrWhiteSpace(classes)
                ? null
                : classes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Ok(_vision.Detect(bytes, confidence, cls, qr));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        { return BadRequest(new { error = "Format gambar tidak didukung (pakai JPG/PNG/WebP)." }); }
        catch (Exception ex) { return StatusCode(500, new { error = $"Deteksi gagal: {ex.Message}" }); }
    }
}
