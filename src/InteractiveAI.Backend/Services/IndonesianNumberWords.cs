using System.Text.RegularExpressions;

namespace InteractiveAI.Backend.Services;

public static class IndonesianNumberWords
{
    private static readonly Dictionary<string, int> Ones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NOL"] = 0, ["KOSONG"] = 0,
        ["SATU"] = 1, ["DUA"] = 2, ["TIGA"] = 3, ["EMPAT"] = 4, ["LIMA"] = 5,
        ["ENAM"] = 6, ["TUJUH"] = 7, ["DELAPAN"] = 8, ["SEMBILAN"] = 9
    };

    private const string Word =
        "(?:NOL|KOSONG|SATU|DUA|TIGA|EMPAT|LIMA|ENAM|TUJUH|DELAPAN|SEMBILAN" +
        "|SEPULUH|SEBELAS|SERATUS|SERIBU|BELAS|PULUH|RATUS|RIBU)";

    private static readonly Regex Run =
        new(@"\b" + Word + @"(?:\s+" + Word + @")*\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsDigitWord(string t) => Ones.ContainsKey(t);

    public static string ToDigits(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Run.Replace(text.ToUpperInvariant(), m => Eval(m.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static string Eval(string[] tokens)
    {

        if (tokens.Length > 1 && tokens.All(IsDigitWord))
            return string.Concat(tokens.Select(t => Ones[t].ToString()));

        long total = 0, cur = 0;
        foreach (var raw in tokens)
        {
            var t = raw.ToUpperInvariant();
            if (Ones.TryGetValue(t, out var d)) { cur += d; continue; }
            switch (t)
            {
                case "SEPULUH": cur += 10; break;
                case "SEBELAS": cur += 11; break;
                case "SERATUS": cur += 100; break;
                case "SERIBU": total += cur == 0 ? 1000 : cur * 1000; cur = 0; break;
                case "BELAS": cur += 10; break;
                case "PULUH": cur = cur == 0 ? 10 : cur * 10; break;
                case "RATUS": cur = cur == 0 ? 100 : cur * 100; break;
                case "RIBU": total += cur == 0 ? 1000 : cur * 1000; cur = 0; break;
            }
        }
        return (total + cur).ToString();
    }
}
