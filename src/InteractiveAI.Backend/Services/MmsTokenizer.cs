using System.Text.Json;

namespace InteractiveAI.Backend.Services;

public class MmsTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private const int PadId = 0;
    private const int UnkId = 35;

    public MmsTokenizer(string vocabPath)
    {
        var json = File.ReadAllText(vocabPath);
        _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                 ?? throw new InvalidOperationException("Gagal membaca vocab.json MMS.");
    }

    public (long[] InputIds, long[] AttentionMask) Encode(string text)
    {
        var ids = new List<long>();
        foreach (var c in (text ?? "").ToLowerInvariant())
        {
            var s = c.ToString();
            if (!_vocab.TryGetValue(s, out var id)) continue;
            ids.Add(PadId);
            ids.Add(id);
        }

        ids.Add(PadId);

        var mask = new long[ids.Count];
        Array.Fill(mask, 1L);
        return (ids.ToArray(), mask);
    }
}