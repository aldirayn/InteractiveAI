using InteractiveAI.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace InteractiveAI.Backend.Controllers;

[ApiController]
[Route("api/master")]
public class MasterController : ControllerBase
{
    private readonly IMasterService _master;
    public MasterController(IMasterService master) => _master = master;

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string nopol, [FromQuery] string? driver,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nopol))
            return BadRequest(new { error = "Query 'nopol' wajib diisi." });
        var r = await _master.LookupAsync(nopol, driver, ct);
        return Ok(new
        {
            found = r.Found,
            nopol = r.Nopol,
            active = r.Active,
            truckType = r.TruckType,
            driverFullName = r.DriverFullName,
            pairMatched = r.PairMatched,
            source = r.Source,
            note = r.Note,
            candidate = r.Candidate,
            nopolHeard = r.NopolHeard,
            matchExact = r.MatchExact,
            driverMissing = r.DriverMissing,
            isTerpairing = r.IsTerpairing,
            driverCode = r.DriverCode
        });
    }
}
