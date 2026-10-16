using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;

public record QrKimoResult(
    bool Checked,
    bool Found,
    string Srd,
    string NoPol,
    string DriverName,
    string DriverCode,
    string TruckType,
    string RepairType,
    string Status,
    bool Usable);

public interface IQrKimoService
{
    Task<QrKimoResult> LookupAsync(string srdText, CancellationToken ct);
}

public class QrKimoService : IQrKimoService
{
    private static readonly HashSet<string> UsableStatus = new(StringComparer.OrdinalIgnoreCase)
        { "DRAFT", "OPEN", "ACCEPT" };
    private static readonly HashSet<string> TerminalStatus = new(StringComparer.OrdinalIgnoreCase)
        { "BATAL", "CANCEL", "DONE" };

    private readonly KacsGpsContext _kacs;
    private readonly KacsGpsOptions _opt;
    private readonly ILogger<QrKimoService> _log;

    public QrKimoService(KacsGpsContext kacs, IOptions<KacsGpsOptions> opt, ILogger<QrKimoService> log)
    {
        _kacs = kacs;
        _opt = opt.Value;
        _log = log;
    }

    public static bool IsUsableStatus(string? status)
    {
        var s = (status ?? "").Trim().ToUpperInvariant();
        if (s.Length == 0) return true;
        if (UsableStatus.Contains(s)) return true;
        if (TerminalStatus.Contains(s)) return false;
        return true;
    }

    public async Task<QrKimoResult> LookupAsync(string srdText, CancellationToken ct)
    {
        var srd = (srdText ?? "").Trim();
        if (srd.Length == 0)
            return new QrKimoResult(false, false, "", "", "", "", "", "", "", false);
        if (string.IsNullOrWhiteSpace(_opt.ConnectionString))
            return new QrKimoResult(false, false, srd, "", "", "", "", "", "", false);
        try
        {
            var row = await _kacs.KimoPerbaikan.AsNoTracking()
                .Where(k => k.Srdnumber == srd)
                .Select(k => new { k.Srdnumber, k.NoPol, k.NamaDriver, k.KodeDriverLama, k.KodeDriver, k.JenisTruck, k.JenisPerbaikan, k.LastStatus })
                .FirstOrDefaultAsync(ct);
            if (row is null)
            {
                _log.LogDebug("QR {Srd} -> tidak terdaftar", srd);
                return new QrKimoResult(true, false, srd, "", "", "", "", "", "", false);
            }
            var usable = IsUsableStatus(row.LastStatus);
            _log.LogDebug("QR {Srd} -> nopol {Nopol} driver {Driver} jenis {Jenis} perbaikan {Perbaikan} status {Status} usable {Usable}",
                srd, row.NoPol ?? "", $"{row.NamaDriver} {row.KodeDriverLama}".Trim(), row.JenisTruck ?? "", row.JenisPerbaikan ?? "", row.LastStatus ?? "", usable);
            return new QrKimoResult(true, true, row.Srdnumber,
                row.NoPol ?? "", row.NamaDriver ?? "",
                string.IsNullOrWhiteSpace(row.KodeDriverLama) ? (row.KodeDriver ?? "") : row.KodeDriverLama!,
                row.JenisTruck ?? "", row.JenisPerbaikan ?? "",
                row.LastStatus ?? "", usable);
        }
        catch (Exception ex)
        {

            _log.LogWarning(ex, "Cek QR KIMO {Srd} gagal, dilewati", srd);
            return new QrKimoResult(false, false, srd, "", "", "", "", "", "", false);
        }
    }
}
