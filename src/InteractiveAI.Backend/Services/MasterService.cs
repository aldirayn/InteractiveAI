using System.Text.RegularExpressions;
using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Data;
using InteractiveAI.Backend.Data.Models.DB.KAGT;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;

public record MasterLookupResult(
    bool Found,
    string Nopol,
    bool Active,
    string TruckType,
    string DriverFullName,
    bool PairMatched,
    string Source,
    string Note,
    string Candidate = "",
    long TruckId = 0,
    long DriverId = 0,
    string NopolHeard = "",
    bool MatchExact = false,
    bool DriverMissing = false,
    int? IsTerpairing = null,
    string DriverCode = "");

public interface IMasterService
{
    Task<MasterLookupResult> LookupAsync(string nopol, string? driver, CancellationToken ct);
}

public class MasterService : IMasterService
{
    private readonly KacsGpsContext _kacs;
    private readonly KacsGpsOptions _opt;
    private readonly ILogger<MasterService> _log;

    public MasterService(KacsGpsContext kacs, IOptions<KacsGpsOptions> opt, ILogger<MasterService> log)
    {
        _kacs = kacs;
        _opt = opt.Value;
        _log = log;
    }

    public static string NormalizeNopol(string? nopol)
    {
        if (string.IsNullOrWhiteSpace(nopol)) return string.Empty;
        return new string(nopol.ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c)).ToArray());
    }

    private static readonly HashSet<string> ProvinceCodes = new(StringComparer.Ordinal)
    {
        "AA","AB","AD","AE","AG","BA","BB","BD","BE","BG","BH","BK","BL","BM","BN",
        "DA","DB","DC","DD","DL","DM","DN","DP","DQ","DR","DT","EA","EB","ED",
        "GA","KB","KH","KT","KU","PA","PB"
    };

    private static bool IsPlausiblePlate(string lead, string digits, string tail)
    {
        if (digits.Length == 0) return false;
        if (lead.Length > 0)
        {

            if (lead.Length > 1 && !ProvinceCodes.Contains(lead)) return false;
        }
        else
        {

            if (digits.Length < 3 || tail.Length < 2) return false;
        }
        return true;
    }

    private static string CompactPlateGroup(string? s) => Regex.Replace(s ?? "", @"\s+", "");

    public static string? ExtractPlateCandidate(string? text)
    {
        var upper = (text ?? "").ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(upper)) return null;
        foreach (Match m in Regex.Matches(upper, @"([A-Z](?:\s*[A-Z])?)\s*(\d(?:\s*\d){0,3})\s*([A-Z](?:\s*[A-Z]){0,2})"))
        {
            var lead = CompactPlateGroup(m.Groups[1].Value);
            var digits = CompactPlateGroup(m.Groups[2].Value);
            var tail = CompactPlateGroup(m.Groups[3].Value);

            if (IsPlausiblePlate(lead, digits, tail))
                return lead + digits + tail;
        }
        foreach (Match m in Regex.Matches(upper, @"(\d(?:\s*\d){1,3})\s*([A-Z](?:\s*[A-Z]){1,2})"))
        {
            var digits = CompactPlateGroup(m.Groups[1].Value);
            var tail = CompactPlateGroup(m.Groups[2].Value);
            if (IsPlausiblePlate("", digits, tail))
                return digits + tail;
        }
        var onlyDigits = Regex.Match(upper, @"\b(\d{2,4})\b");
        if (onlyDigits.Success) return onlyDigits.Groups[1].Value;
        return null;
    }

    public async Task<MasterLookupResult> LookupAsync(string nopol, string? driver, CancellationToken ct)
    {

        _log.LogDebug("Lookup masuk nopol {Nopol} driver {Driver}", nopol ?? "", driver ?? "");
        var converted = IndonesianNumberWords.ToDigits(nopol ?? "");
        var candidate = ExtractPlateCandidate(converted);
        if (string.IsNullOrEmpty(candidate) || !candidate.Any(char.IsDigit))
            return new MasterLookupResult(false, nopol ?? "", false, "", "", false, "vwallunitv3",
                "Tidak ada pola nopol pada jawaban.", "");
        if (string.IsNullOrWhiteSpace(_opt.ConnectionString))
            return new MasterLookupResult(false, nopol ?? "", false, "", "", false, "vwallunitv3",
                "Master KacsGps belum dikonfigurasi.", candidate);
        try
        {
            var result = await LookupViewAsync(candidate, nopol ?? "", driver, ct);
            _log.LogDebug("Lookup hasil nopol {Nopol} driver {Driver} -> found {Found} nopolDb {NopolDb} aktif {Active} jenis {Jenis} nama {Nama} pair {Pair} exact {Exact} driverMissing {Missing} note {Note}",
                nopol ?? "", driver ?? "", result.Found, result.Nopol, result.Active, result.TruckType, result.DriverFullName, result.PairMatched, result.MatchExact, result.DriverMissing, result.Note);
            return result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Lookup view vwallunitv3 {Nopol} gagal", candidate);
            return new MasterLookupResult(false, nopol ?? "", false, "", "", false, "vwallunitv3",
                $"Bank data tak terjangkau: {ex.Message}", candidate);
        }
    }

    private record ViewHit(string? NoPol, string? NoPol2, string? JenisTruck, string? NamaDriver,
        string? KodeDriver, string? KodeDriverOld, string? TmsStatus, string? DriverStatus, int? IsTerpairing);

    private async Task<MasterLookupResult> LookupViewAsync(string norm, string rawNopol,
        string? driver, CancellationToken ct)
    {
        var heard = ParsePlate(norm);
        var like = "%" + string.Join("%", heard.Digits.Select(c => c.ToString())) + "%";
        var rows = await _kacs.VwAllUnitV3s.AsNoTracking()
            .Where(x => x.NoPol2 != null && EF.Functions.Like(x.NoPol2, like))
            .Select(x => new ViewHit(x.NoPol, x.NoPol2, x.JenisTruck, x.NamaDriver, x.KodeDriver,
                x.KodeDriverOld, x.TmsStatus, x.DriverStatus, x.IsTerpairing))
            .ToListAsync(ct);
        _log.LogDebug("Lookup {Norm}: {N} kandidat view dari LIKE", norm, rows.Count);

        ViewHit? best = null;
        var bestScore = int.MaxValue;
        foreach (var t in rows)
        {
            var stored = string.IsNullOrWhiteSpace(t.NoPol2) ? t.NoPol : t.NoPol2;
            var cand = ParsePlate(NormalizeNopol(stored));
            if (!cand.Digits.Equals(heard.Digits, StringComparison.Ordinal)) continue;
            if (!LeadOk(heard.Lead, cand.Lead)) continue;
            var dist = Levenshtein(LetterKey(heard.Tail), LetterKey(cand.Tail));
            if (dist > 1) continue;
            var score = dist * 10 + (heard.Lead.Length == 0 ? 1 : 0);
            if (score >= bestScore) continue;
            bestScore = score;
            best = t;
        }
        if (best is null)
            return new MasterLookupResult(false, rawNopol, false, "", "", false, "vwallunitv3",
                $"Nopol {rawNopol} tidak ditemukan di view unit.", norm);

        var display = string.IsNullOrWhiteSpace(best.NoPol) ? norm : best.NoPol!;
        var truckType = best.JenisTruck ?? "";
        var fullName = best.NamaDriver ?? "";
        var matched = ViewNameMatches(driver, fullName, best.KodeDriver, best.KodeDriverOld, norm,
            out var nameProvided) && !string.IsNullOrWhiteSpace(fullName);
        return new MasterLookupResult(
            Found: true,
            Nopol: display,
            Active: IsActiveStatus(best.TmsStatus, best.DriverStatus),
            TruckType: truckType,
            DriverFullName: fullName,
            PairMatched: matched,
            Source: "vwallunitv3",
            Note: matched
                ? $"Nopol {display} terdaftar di view (jenis {truckType}, driver {fullName})."
                : $"Nopol {display} terdaftar di view" + (nameProvided
                    ? $" tapi driver \"{driver}\" berbeda dengan {fullName}."
                    : " (nama driver belum disebutkan)."),
            Candidate: norm,
            NopolHeard: norm,
            MatchExact: NormalizeNopol(display).Equals(NormalizeNopol(norm), StringComparison.Ordinal),
            DriverMissing: !matched && !nameProvided,
            IsTerpairing: best.IsTerpairing,
            DriverCode: best.KodeDriver ?? "");
    }

    private static bool ViewNameMatches(string? driverText, string? viewName, string? kode,
        string? kodeOld, string plateCandidate, out bool nameProvided)
    {
        var tokens = DriverTokens(driverText, plateCandidate);
        nameProvided = tokens.Count > 0;
        if (!nameProvided) return false;
        var vn = (viewName ?? "").ToUpperInvariant();
        var words = vn.Split(new[] { ' ', '-', '.', ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            if (t.Equals((kode ?? "").ToUpperInvariant(), StringComparison.Ordinal)) return true;
            if (t.Equals((kodeOld ?? "").ToUpperInvariant(), StringComparison.Ordinal)) return true;
            if (vn.Contains(t, StringComparison.Ordinal)) return true;
            if (words.Any(w => w.Contains(t, StringComparison.Ordinal) || t.Contains(w, StringComparison.Ordinal))) return true;
            if (t.Length >= 4 && words.Any(w => w.Length >= 4 && Levenshtein(t, w) <= 1)) return true;
        }
        return false;
    }

    private static bool IsActiveStatus(string? status, string? statusflow)
    {
        var s = ((status ?? "") + " " + (statusflow ?? "")).ToLowerInvariant();
        if (s.Contains("nonaktif") || s.Contains("non-aktif") || s.Contains("inactive")
            || s.Contains("blok") || s.Contains("jual"))
            return false;
        return true;
    }

    private static readonly HashSet<string> DriverStopWords = new(StringComparer.Ordinal)
    {
        "SAYA","SAYAAN","PAK","PAKMU","BAPAK","BAPAKU","KAK","KAKI","ANDA","NAMA","NAMAAN","KODE",
        "DRIVER","DRIVERNAME","NOPOL","NOPOLNYA","TRUK","YANG","INI","ITU","UNTUK","DARI","KE",
        "DI","ADALAH","ATAS","POL","UNIT","KASIH","TERIMA","TERIMAKASIH","MISI","BENGKEL","LOKET",
        "MASIH","BELUM","SUDAH","UNTUNG","SOPIR","JUGA","DAN","ATAU","TOLONG","TOLONGLAH","YA","ABANG", "BANG"
    };

    private static List<string> DriverTokens(string? driver, string plateCandidate)
    {
        var plate = NormalizeNopol(plateCandidate);
        var parts = Regex.Split((driver ?? "").ToUpperInvariant(), "[^A-Z0-9]+");
        var outv = new List<string>();
        foreach (var raw in parts)
        {
            var t = raw.Trim();
            if (t.Length < 3) continue;
            if (DriverStopWords.Contains(t)) continue;
            var hasLetter = t.Any(char.IsLetter);
            var hasDigit = t.Any(char.IsDigit);
            if (hasLetter && hasDigit) continue;
            if (plate.Length > 0 && plate.Contains(t, StringComparison.Ordinal)) continue;
            if (!outv.Contains(t)) outv.Add(t);
        }
        return outv.Take(6).ToList();
    }

    private record PlateParts(string Lead, string Digits, string Tail);

    private static PlateParts ParsePlate(string norm)
    {
        var m = Regex.Match(norm ?? "", @"^([A-Z]*)(\d+)([A-Z]*)$");
        return m.Success
            ? new PlateParts(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)
            : new PlateParts("", "", norm ?? "");
    }

    private static string LetterKey(string s) =>
        (s ?? "").ToUpperInvariant().Replace("KS", "X").Replace("Z", "S");

    private static bool LeadOk(string heardLead, string storedLead)
    {
        if (heardLead.Length == 0) return true;
        if (heardLead.Equals(storedLead, StringComparison.Ordinal)) return true;
        return Levenshtein(heardLead, storedLead) <= 1;
    }

    private static int Levenshtein(string a, string b)
    {
        a ??= ""; b ??= "";
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
