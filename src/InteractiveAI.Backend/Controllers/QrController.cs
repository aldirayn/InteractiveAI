using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/qr")]
public class QrController : ControllerBase
{
    private readonly IQrKimoService _qr;
    public QrController(IQrKimoService qr) => _qr = qr;

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string srd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(srd))
            return BadRequest(new { error = "Query 'srd' wajib diisi." });
        var r = await _qr.LookupAsync(srd, ct);
        return Ok(new
        {
            @checked = r.Checked,
            found = r.Found,
            srd = r.Srd,
            nopol = r.NoPol,
            driverName = r.DriverName,
            driverCode = r.DriverCode,
            truckType = r.TruckType,
            repairType = r.RepairType,
            status = r.Status,
            usable = r.Usable
        });
    }
}
