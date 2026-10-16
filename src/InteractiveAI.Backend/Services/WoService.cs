using InteractiveAI.Backend.Data;
using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Data.Models.DB.KAGT;
using InteractiveAI.Backend.DTOs;
using InteractiveAI.Backend.Models.DB;
using InteractiveAI.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Collections;

namespace InteractiveAI.Backend.Services;

public interface IWoService
{
    Task<WorkOrder> CreateFromSessionAsync(Guid sessionId, CancellationToken ct);
    Task<List<WorkOrder>> ListAsync(CancellationToken ct);
    Task<WorkOrder> GetAsync(long id, CancellationToken ct);
    Task<bool> KimoExistsAsync(string srd, CancellationToken ct);
    Task<List<KimoWoRow>> KimoWoListAsync(string srd, CancellationToken ct);
}

public record KimoWoRow(string WoNo, string WoDate, string Nopol, string Jenis, string Driver, string Keluhan);


public class WoService : IWoService
{

    private const int InitialPreStatus = 0;
    private const short InitialTrxtype = 0;

    private readonly AppDbContext _db;
    private readonly MkaPoolContext _mka;
    private readonly KacsGpsContext _kacs;
    private readonly IMasterService _master;
    private readonly IQrKimoService _qr;
    private readonly IOllamaService _ollama;
    private readonly ILogger<WoService> _log;
    private readonly WoOptions _woOpt;

    public WoService(AppDbContext db, MkaPoolContext mka, KacsGpsContext kacs,
        IMasterService master, IQrKimoService qr, IOllamaService ollama, ILogger<WoService> log,
        IOptions<WoOptions> woOpt)
    {
        _db = db;
        _mka = mka;
        _kacs = kacs;
        _master = master;
        _qr = qr;
        _ollama = ollama;
        _log = log;
        _woOpt = woOpt.Value;
    }

    private static void AppendKeluhan(Dictionary<string, string> map, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
        if (map.TryGetValue(key, out var cur) && !string.IsNullOrWhiteSpace(cur))
            map[key] = cur + "; " + value.Trim();
        else
            map[key] = value.Trim();
    }

    private string ResolvePrefix(string category)
    {
        var prefix = _woOpt.CategoryPrefixes.TryGetValue(category, out var p) && !string.IsNullOrWhiteSpace(p)
            ? p.Trim().ToUpperInvariant()
            : _woOpt.DefaultPrefix.Trim().ToUpperInvariant();
        if (prefix.Length == 0 || prefix.Length > Math.Max(1, _woOpt.MaxPrefixLength))
            throw new InvalidOperationException(
                $"Prefix WO untuk kategori \"{category}\" tidak valid: \"{prefix}\".");
        return prefix;
    }

    private string NormalizeCategory(string? raw)
    {
        var key = System.Text.RegularExpressions.Regex
            .Replace((raw ?? "").Trim().ToUpperInvariant(), @"\s+", " ");
        if (key.Length == 0) return _woOpt.DefaultCategory;

        foreach (var c in _woOpt.CanonicalCategories)
            if (c.Equals(key, StringComparison.OrdinalIgnoreCase)) return c;

        if (_woOpt.CategoryAliases.TryGetValue(key, out var aliased)
            && !string.IsNullOrWhiteSpace(aliased))
            return aliased.Trim();

        foreach (var alias in _woOpt.CategoryAliases)
        {
            var needle = System.Text.RegularExpressions.Regex.Escape(alias.Key.Trim().ToUpperInvariant());
            if (needle.Length == 0) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(key, @"\b" + needle + @"\b"))
                return alias.Value.Trim();
        }

        foreach (var c in _woOpt.CanonicalCategories)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                key, @"\b" + System.Text.RegularExpressions.Regex.Escape(c.ToUpperInvariant()) + @"\b"))
                return c;
        }

        _log.LogWarning("Kategori \"{Raw}\" tak dikenal, dipakai default {Default}", raw, _woOpt.DefaultCategory);
        return _woOpt.DefaultCategory;
    }

    public async Task<WorkOrder> CreateFromSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.Sessions.Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException("Session tidak ditemukan.");

        var transcript = session.Messages
            .OrderBy(m => m.Id)
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => $"{m.Role}: {m.Content}");
        var qr = session.Messages
            .Where(m => (m.Role is "user" or "assistant") && m.Content.Contains("[QR KIMO:"))
            .Select(m => NormSrd(ExtractQr(m.Content)))
            .LastOrDefault(q => q.Length > 0) ?? string.Empty;

        var (driver, driverCode, unit, categories, complaint, parking) =
            await ExtractAsync(session.Model, string.Join("\n", transcript), ct);

        var transcriptText = string.Join("\n", transcript);
        var verified = LastVerifiedMarker(transcriptText);
        if (verified is not null)
        {
            unit = verified.Value.Norm;
            if (!string.IsNullOrWhiteSpace(verified.Value.FullName) && verified.Value.FullName != "-")
                driver = verified.Value.FullName;
        }

        var formRows = ParseKeluhanRows(transcriptText);
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in formRows)
            AppendKeluhan(canonical, NormalizeCategory(kv.Key), kv.Value);
        foreach (var seg in SplitComplaintSegments(complaint))
            AppendKeluhan(canonical, NormalizeCategory(seg.Key), seg.Value);

        var keluhanMap = canonical;
        if (keluhanMap.Count > 0)
        {
            categories = string.Join(", ", keluhanMap.Keys);
            complaint = string.Join("; ", keluhanMap.Select(kv => $"[{kv.Key}] {kv.Value}"));
        }

        if (transcriptText.LastIndexOf("[LANJUT_AGENT]", StringComparison.OrdinalIgnoreCase)
            > transcriptText.LastIndexOf("[QR KIMO:", StringComparison.Ordinal))
            qr = string.Empty;

        var qrRow = string.IsNullOrWhiteSpace(qr)
            ? new QrKimoResult(false, false, "", "", "", "", "", "", "", false)
            : await _qr.LookupAsync(qr, ct);

        if (string.IsNullOrWhiteSpace(unit) && qrRow.Usable) unit = qrRow.NoPol;
        if (string.IsNullOrWhiteSpace(driver) && string.IsNullOrWhiteSpace(driverCode) && qrRow.Usable)
        {
            driver = qrRow.DriverName;
            driverCode = qrRow.DriverCode;
        }

        var master = await _master.LookupAsync(unit, $"{driver} {driverCode}".Trim(), ct);
        var masterCtx = string.IsNullOrWhiteSpace(master.Note) ? "" : $" Catatan: {master.Note} [{master.Source}].";
        var driverWins = transcriptText.Contains("[NAMA_SOPIR]", StringComparison.OrdinalIgnoreCase);

        if (!master.Found)
            throw new InvalidOperationException(
                $"Nopol \"{unit}\" tidak terdaftar di view unit (kacs_gps) — WO tidak dibuat.{masterCtx}");

        if (!driverWins && !master.PairMatched && !master.DriverMissing)
            throw new InvalidOperationException(
                $"Driver \"{driver}\" tidak cocok dengan driver nopol \"{unit}\" di view unit (kacs_gps) — WO tidak dibuat.{masterCtx}");

        var interviewWins = transcriptText.Contains("[INTERVIEW_MENANG]",
            StringComparison.OrdinalIgnoreCase);
        var qrWins = qrRow is { Checked: true, Found: true, Usable: true } && !interviewWins;
        if (qrWins)
        {
            var qMaster = await _master.LookupAsync(qrRow.NoPol, qrRow.DriverName, ct);
            if (qMaster.Found && qMaster.PairMatched)
                master = qMaster;
        }

        if (!qrWins && !master.MatchExact)
            throw new InvalidOperationException(
                $"Nopol \"{unit}\" yang tercatat tak cocok persis dengan unit di bank data "
                + $"(\"{master.Nopol}\") — WO tidak dibuat demi mencegah nopol keliru terekam. "
                + $"Minta sopir mengonfirmasi nopolnya atau arahkan ke loket WO manusia.{masterCtx}");

        var displayDriver = qrRow is { Checked: true, Found: true, Usable: true }
            && !interviewWins && !string.IsNullOrWhiteSpace(qrRow.DriverName)
            ? qrRow.DriverName.Trim()
            : driverWins && !string.IsNullOrWhiteSpace(driver) ? driver
            : master.DriverFullName;

        var nopolKey = master.Nopol.Trim();
        var carry = await _mka.TrWoHdrHistories.AsNoTracking()
            .Where(x => x.PreUnitNo == nopolKey)
            .OrderByDescending(x => x.Creatddt)
            .ThenByDescending(x => x.Createtime)
            .Select(x => new HistoryCarry(
                x.PreLastMaintenDate, x.PreLastMaintenTime,
                x.PreLastKmActual, x.PreLastHmActual,
                x.PreKmGps, x.PreHmGps,
                x.PreLastQcDate, x.PreLastQcTime,
                x.PreLastQcKmGps, x.PreLastQcKmActual,
                x.PreLastQcHmGps, x.PreLastQcHmActual))
            .FirstOrDefaultAsync(ct);

        var now = DateTime.Now;

        decimal gpsKm = 0m, gpsHm = 0m;
        short gpsStatus = 0;
        try
        {
            var platePlain = MasterService.NormalizeNopol(master.Nopol);
            var plateSpaced = string.Join(" ",
                master.Nopol.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToUpperInvariant();
            var gpsLast = await (from g in _kacs.GpsHistoryLasts.AsNoTracking()
                                 where g.Plate == platePlain || g.Plate == plateSpaced
                                 orderby g.TimeLocal descending
                                 select g).FirstOrDefaultAsync(ct);

            if (gpsLast != null)
            {
                gpsKm = (decimal)(gpsLast.Mileage ?? 0);
                gpsHm = gpsLast.Hourmeter ?? 0m;
                gpsStatus = 1;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "GPS lookup {Nopol} gagal, pakai default 0", master.Nopol);
        }

        var rows = new List<DictionaryEntry>();
        var woNumbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var primary = "";

        if (keluhanMap.Count == 0)
        {
            var fallbackKey = NormalizeCategory(categories);
            var fallbackVal = string.IsNullOrWhiteSpace(complaint) ? "Keluhan tidak disebutkan" : complaint.Trim();
            AppendKeluhan(keluhanMap, fallbackKey, fallbackVal);
        }

        await using var trx = await _mka.Database.BeginTransactionAsync(ct);
        try
        {
            if (qr.Length > 0)
            {
                _log.LogInformation("Cek duplikat SRD '{Srd}' sebelum simpan WO sesi {SessionId}", qr, sessionId);
                var dup = await _mka.TrWoHdrProcess.AnyAsync(
                    x => x.PreSaranPm == qr.Trim(), ct);
                if (dup)
                    throw new InvalidOperationException(
                        $"WO untuk SRDNumber '{qr.Trim()}' sudah pernah dibuat.");

                var dupH = await _mka.TrWoHdrHistories.AnyAsync(
                   x => x.PreSaranPm == qr.Trim(), ct);
                if (dupH)
                    throw new InvalidOperationException(
                        $"WO untuk SRDNumber '{qr.Trim()}' sudah pernah dibuat.");
            }

            foreach (var kv in keluhanMap)
            {
                var cat = kv.Key;
                var isi = kv.Value;
                var prefix = ResolvePrefix(cat);

                var numbers = await _mka.Database
                    .SqlQueryRaw<string>("CALL sp_get_new_running_no({0})", prefix)
                    .ToListAsync(ct);

                var srNo = (numbers.FirstOrDefault() ?? "").Trim();
                if (srNo.Length == 0 || srNo.Length > 15)
                    throw new InvalidOperationException(
                        $"SP sp_get_new_running_no tidak mengembalikan nomor valid untuk prefix \"{prefix}\": \"{srNo}\".");

                if (primary.Length == 0) primary = srNo;
                woNumbers[cat] = srNo;

                var hdr = BuildHeader(srNo, now, master, driverCode.Trim(), cat,
                    isi, parking.Trim(), qr, displayDriver, carry, gpsKm, gpsHm, gpsStatus);

                _mka.TrWoHdrProcess.Add(hdr);
                rows.Add(new DictionaryEntry(cat, srNo));
            }

            await _mka.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);
        }
        catch
        {
            await trx.RollbackAsync(ct);
            throw;
        }

        var wo = new WorkOrder
        {
            WoNumber = primary,
            SessionId = sessionId,
            Model = session.Model,
            DriverName = displayDriver,
            DriverCode = driverCode.Trim(),
            UnitNumber = master.Nopol,
            RepairCategories = string.Join(", ", woNumbers.Keys),
            Complaint = complaint.Trim(),
            ParkingLocation = parking.Trim(),
            QrKimo = qr,
            Status = "OPEN",
            KeluhanRows = keluhanMap.Count > 0 ? keluhanMap : null,
            WoNumbers = woNumbers.Count > 0 ? woNumbers : null,
            WoCount = woNumbers.Count
        };

        try
        {
            foreach (DictionaryEntry row in rows)
            {
                var cat = (string)row.Key!;
                var no = (string)row.Value!;
                var mirror = new WorkOrder
                {
                    WoNumber = no,
                    SessionId = sessionId,
                    Model = session.Model,
                    DriverName = displayDriver,
                    DriverCode = driverCode.Trim(),
                    UnitNumber = master.Nopol,
                    RepairCategories = cat,
                    Complaint = keluhanMap.TryGetValue(cat, out var mc) ? mc : "",
                    ParkingLocation = parking.Trim(),
                    QrKimo = qr,
                    Status = "OPEN",
                    KeluhanRows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [cat] = keluhanMap.TryGetValue(cat, out var mcv) ? mcv : ""
                    }
                };
                _db.WorkOrders.Add(mirror);
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Mirror lokal WO {SrNo} gagal (WO pool tetap tercatat)", primary);
        }

        return wo;
    }


    private record HistoryCarry(
        DateTime MaintenDate, DateTime MaintenTime,
        decimal KmActual, decimal HmActual, decimal KmGps, decimal HmGps,
        DateTime QcDate, DateTime QcTime,
        decimal QcKmGps, decimal QcKmActual, decimal QcHmGps, decimal QcHmActual);

    private static string Trunc(string? s, int max)
    {
        s ??= "";
        return s.Length <= max ? s : s[..max];
    }


    private static List<string> SplitKeluhan(string complaint)
    {
        var parts = System.Text.RegularExpressions.Regex
            .Split(complaint ?? "", @"(?<=[.!?;\n])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var out_ = new List<string>();
        var buf = "";

        foreach (var p in parts)
        {
            if (p.Length > 51)
            {
                if (buf.Length > 0) { out_.Add(buf); buf = ""; }

                for (var i = 0; i < p.Length && out_.Count < 12; i += 51)
                    out_.Add(p.Substring(i, Math.Min(51, p.Length - i)));
                continue;
            }

            if (buf.Length == 0) buf = p;
            else if (buf.Length + 1 + p.Length <= 51) buf += " " + p;
            else
            {
                out_.Add(buf);
                if (out_.Count >= 12) { buf = ""; break; }
                buf = p;
            }

            if (out_.Count >= 12) break;
        }

        if (buf.Length > 0 && out_.Count < 12) out_.Add(buf);

        return out_;
    }

    private static TrWoHdrProcess BuildHeader(string srNo, DateTime now, MasterLookupResult master,
        string driverCode, string categories, string complaint, string parking, string qr,
        string displayDriver, HistoryCarry? carry, decimal gpsKm, decimal gpsHm, short gpsStatus)
    {
        var keluhan = SplitKeluhan(complaint);
        string K(int i) => i < keluhan.Count ? keluhan[i] : "";

        var empty1900 = new DateTime(1900, 1, 1, 0, 0, 0);
        var woDate = now.Date;
        var woTime = empty1900.Add(now.TimeOfDay);

        var h = new TrWoHdrProcess
        {
            PreSrNo = "",
            PreSrcNo = Trunc(srNo, 15),
            PreRedoNo = "",
            PreWoDate = woDate,
            PreWoTime = woTime,
            PreWoEndDate = empty1900,
            PreWoEndTime = empty1900,
            PreUnitId = Trunc(master.TruckId > 0 ? master.TruckId.ToString() : "", 13),
            PreUnitNo = Trunc(master.Nopol, 33),
            PreJenis = Trunc(master.TruckType, 51),
            PreMerk = "",
            PreModel = "",
            PreReqBy = Trunc(displayDriver, 101),
            PreDriverId = Trunc(!string.IsNullOrWhiteSpace(master.DriverCode) ? master.DriverCode : (master.DriverId > 0 ? master.DriverId.ToString() : ""), 35),
            PreDriverName = Trunc(displayDriver, 101),
            PrePriorityId = "",
            PrePriorityName = "",
            PrePriorityDesc = "",
            PreLastMaintenDate = carry?.MaintenDate ?? now,
            PreLastMaintenTime = carry?.MaintenTime ?? now,
            PreLastKmActual = gpsStatus == 1 && gpsKm > 0 ? gpsKm : (carry?.KmActual ?? 0),
            PreStatusGps = gpsStatus,
            PreLastUpdate = empty1900,
            PreLastUpdateTime = empty1900,
            PreKmGps = gpsStatus == 1 ? gpsKm : 0,
            PreHmGps = gpsHm,
            PreKmActual = gpsStatus == 1 && gpsKm > 0 ? gpsKm : (carry?.KmActual ?? 0),
            PreHmActual = gpsHm > 0 ? gpsHm : (carry?.HmActual ?? 0),
            PrePerbaikanDesc = $"[{categories}] {complaint}".Trim(),
            PreKeluhan1 = K(0),
            PreKeluhan2 = K(1),
            PreKeluhan3 = K(2),
            PreKeluhan4 = K(3),
            PreKeluhan5 = K(4),
            PreKeluhan6 = K(5),
            PreKeluhan7 = K(6),
            PreKeluhan8 = K(7),
            PreKeluhan9 = K(8),
            PreKeluhan10 = K(9),
            PreKeluhan11 = K(10),
            PreKeluhan12 = K(11),
            PrePoolId = null,
            PrePoolName = "",
            PreRemarksPool = "",
            PreBayId = "",
            PreBayName = "",
            PreSaranPm = string.IsNullOrWhiteSpace(qr) ? " " : Trunc(qr, 101),
            PreEstimasi = string.Empty,
            PreEstimasiDate = empty1900,
            PreEstimasiTime = empty1900,
            PreRev = 0,
            PreRemarks = string.IsNullOrWhiteSpace(qr) ? "" : Trunc($"QR KIMO: {qr}", 1000),
            PreStatus = InitialPreStatus,
            Interid = "MKA",
            Crusrid = "LOKET",
            Creatddt = now,
            Mdfusrid = "LOKET",
            Modifdt = now,
            PreRejectType = 0,
            PreReject = "",
            Createtime = now,
            PreJnsPerbaikan = Trunc(categories, 33),
            PreGroup = "",
            PreSubJobId = "",
            PreSubJobDesc = "",
            SubJobDurationJam = "",
            SubJobDurationMenit = "",
            PreLastQcDate = carry?.QcDate ?? now,
            PreLastQcTime = carry?.QcTime ?? now,
            PreLastKmGps = carry?.QcKmGps ?? 0,
            PreLastHmActual = carry?.HmActual ?? 0,
            PreLastHmGps = carry?.QcHmGps ?? 0,
            PreLastQcKmGps = carry?.QcKmGps ?? 0,
            PreLastQcKmActual = carry?.QcKmActual ?? 0,
            PreLastQcHmGps = carry?.QcHmGps ?? 0,
            PreLastQcHmActual = carry?.QcHmActual ?? 0,
            PreOhEngine = 0,
            PreQuickCheck = 0,
            PreExternal = 0,
            Trxtype = InitialTrxtype,
            Modiftime = now,
            DexRowId = 0,
            NoHpDrv = "",
            DeskId = 2075,
            LokasiParkir = Trunc(parking, 100),
        };

        return h;
    }

    private async Task<(string driver, string driverCode, string unit, string categories, string complaint, string parking)>
        ExtractAsync(string? model, string transcript, CancellationToken ct)
    {

        var extract = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content =
                "Ekstrak data Work Order bengkel truk dari transkrip berikut. " +
                "Balas HANYA dengan JSON valid tanpa markdown: " +
                "{\"driver\": \"nama sopir\", \"driverCode\": \"kode driver\", \"unit\": \"nopol/nomor unit truk\", " +
                "\"categories\": \"kategori perbaikan, WAJIB salah satu dari: Truk, Reefer, Cabin, GPS, Genset, Box, Ban (pisahkan dengan koma)\", " +
                "Aturan kategori: Reefer = sistem pendingin/refrigerasi, TERMASUK keluhan AC (air conditioner) tidak dingin atau bocor freon; " +
                "Cabin = kabin kemudi di luar pendingin (jok, kaca, pintu, dashboard). " +
                "\"complaint\": \"permintaan/keluhan perbaikan detail dari UCAPAN SUARA saja\", " +
                "\"parking\": \"lokasi parkir truk\"}. " +
                "Baris pesan pengguna berbentuk [KELUHAN Kategori: isi] adalah data form per kategori yang diketik: JANGAN ulangi isinya di complaint. " +
                "Bila satu kategori disebut berkali-kali (mis. ada tambahan untuk truk), gabung semua detailnya dengan titik-koma tanpa menghilangkan apa pun. " +
                "untuk categories gabung daftar kategorinya, untuk complaint gabung semua keluhan dari ucapan sebagai \"[Kat] isi; ...\". " +
                "Abaikan fragmen ejaan (mis. \"B 1\"); untuk nopol pakai yang lengkap " +
                "terakhir yang terverifikasi. " +
                "Jika info tidak ada, isi string kosong." },
            new() { Role = "user", Content = transcript }
        };

        string raw;
        try
        {
            raw = await _ollama.ChatAsync(
                string.IsNullOrWhiteSpace(model) ? "gpt-oss:120b" : model,
                extract, 0.1, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Gagal menghubungi Ollama: {ex.Message}", ex);
        }

        string driver = "", driverCode = "", unit = "", categories = "", complaint = "", parking = "";
        try
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
                driver = doc.RootElement.TryGetProperty("driver", out var d) ? d.GetString() ?? "" : "";
                driverCode = doc.RootElement.TryGetProperty("driverCode", out var dc) ? dc.GetString() ?? "" : "";
                unit = doc.RootElement.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
                categories = doc.RootElement.TryGetProperty("categories", out var cg) ? cg.GetString() ?? "" : "";
                complaint = doc.RootElement.TryGetProperty("complaint", out var c) ? c.GetString() ?? "" : "";
                parking = doc.RootElement.TryGetProperty("parking", out var p) ? p.GetString() ?? "" : "";
            }
        }
        catch { }

        return (driver.Trim(), driverCode.Trim(), unit.Trim(), categories.Trim(), complaint.Trim(), parking.Trim());
    }

    private static Dictionary<string, string> ParseKeluhanRows(string transcriptText)
    {
        var out_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(transcriptText)) return out_;
        const string head = "user: [KELUHAN ";
        foreach (var line in transcriptText.Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith(head, StringComparison.OrdinalIgnoreCase)) continue;
            var close = t.IndexOf(']', head.Length);
            var colon = close > 0 ? t.IndexOf(':', close) : -1;
            if (close < 0 || colon < 0) continue;
            var cat = t.Substring(head.Length, close - head.Length).Trim();
            var val = t.Substring(colon + 1).Trim().TrimEnd(']');
            if (cat.Length == 0 || val.Length == 0) continue;
            if (out_.TryGetValue(cat, out var cur))
                out_[cat] = cur + "; " + val;
            else
                out_[cat] = val;
        }
        return out_;
    }

    private static Dictionary<string, string> SplitComplaintSegments(string complaint)
    {
        var out_ = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(complaint)) return out_;
        foreach (var seg in complaint.Split(';'))
        {
            var t = seg.Trim();
            if (t.Length == 0) continue;
            var m = System.Text.RegularExpressions.Regex.Match(t, @"^\[([^\]]+)\]\s*(.+)$");
            var cat = m.Success ? m.Groups[1].Value.Trim() : "Umum";
            var val = (m.Success ? m.Groups[2].Value : t).Trim();
            if (cat.Length == 0 || val.Length == 0) continue;
            if (out_.TryGetValue(cat, out var cur))
                out_[cat] = cur + "; " + val;
            else
                out_[cat] = val;
        }
        return out_;
    }

    // Ambil penanda verifikasi terakhir "(kode: NORM | sopir: NAMA)" dari transkrip.
    // Mengembalikan null bila tak ada (fallback: nilai LLM).
    private static (string Norm, string FullName)? LastVerifiedMarker(string transcriptText)
    {
        if (string.IsNullOrEmpty(transcriptText)) return null;
        var m = System.Text.RegularExpressions.Regex.Matches(transcriptText,
            @"\(kode:\s*([A-Z0-9]+)\s*\|\s*sopir:\s*([^)]+)\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (m.Count == 0) return null;
        var last = m[m.Count - 1];
        var norm = last.Groups[1].Value.Trim().ToUpperInvariant();
        var name = last.Groups[2].Value.Trim();

        if (norm.Length == 0) 
            return null;

        return (norm, name);
    }

    private static string NormSrd(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);

        foreach (var c in s)
        {
            if (char.IsControl(c)) continue;
            var cat = char.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.Format || cat == UnicodeCategory.Control
                || cat == UnicodeCategory.Surrogate) continue;
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    private static string ExtractQr(string content)
    {
        const string marker = "[QR KIMO:";
        var i = content.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return string.Empty;
        var j = content.IndexOf(']', i);

        return (j > i ? content[(i + marker.Length)..j] : content[(i + marker.Length)..]).Trim();
    }

    public async Task<bool> KimoExistsAsync(string srd, CancellationToken ct)
    {
        var key = NormSrd(srd);
        if (key.Length == 0) return false;
        if (await _mka.TrWoHdrProcess.AsNoTracking().AnyAsync(x => x.PreSaranPm == key, ct))
            return true;
        return await _mka.TrWoHdrHistories.AsNoTracking().AnyAsync(x => x.PreSaranPm == key, ct);
    }

    public async Task<List<KimoWoRow>> KimoWoListAsync(string srd, CancellationToken ct)
    {
        var out_ = new List<KimoWoRow>();
        var key = NormSrd(srd);
        if (key.Length == 0) return out_;
        out_.AddRange(await _mka.TrWoHdrProcess.AsNoTracking()
            .Where(x => x.PreSaranPm == key)
            .OrderByDescending(x => x.PreWoDate)
            .Take(20)
            .Select(x => new KimoWoRow(x.PreSrcNo, x.PreWoDate.ToString("dd-MM-yyyy"), x.PreUnitNo,
                x.PreJenis, x.PreDriverName, x.PrePerbaikanDesc))
            .ToListAsync(ct));
        if (out_.Count < 20)
            out_.AddRange(await _mka.TrWoHdrHistories.AsNoTracking()
                .Where(x => x.PreSaranPm == key)
                .OrderByDescending(x => x.PreWoDate)
                .Take(20 - out_.Count)
                .Select(x => new KimoWoRow(x.PreSrcNo, x.PreWoDate.ToString("dd-MM-yyyy"), x.PreUnitNo,
                    x.PreJenis, x.PreDriverName, x.PrePerbaikanDesc))
                .ToListAsync(ct));
        return out_;
    }

    public async Task<List<WorkOrder>> ListAsync(CancellationToken ct) =>
        await _db.WorkOrders.OrderByDescending(w => w.Id).Take(100).ToListAsync(ct);

    public async Task<WorkOrder> GetAsync(long id, CancellationToken ct) =>
        await _db.WorkOrders.FindAsync(new object[] { id }, ct)
        ?? throw new KeyNotFoundException("WO tidak ditemukan.");
}
