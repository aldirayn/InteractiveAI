namespace InteractiveAI.Backend.Services;

public class VitsTokenizer
{
    private static readonly string[] TokenOrder =
    {
        "<PAD>", " ", "!", ",", ".", "?", "a", "b", "d", "e", "f", "h", "i",
        "j", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "w", "x", "z",
        "ŋ", "ɔ", "ə", "ɛ", "ɡ", "ɪ", "ɲ", "ʃ", "ʊ", "ʒ", "ʔ", "ˈ", "<BLNK>"
    };

    private readonly Dictionary<string, int> _vocab;
    private const int BlankId = 39;

    public VitsTokenizer()
    {
        _vocab = new Dictionary<string, int>(TokenOrder.Length);
        for (int i = 0; i < TokenOrder.Length; i++) _vocab[TokenOrder[i]] = i;
    }

    public long[] Encode(string text)
    {
        var ids = new List<long>();
        foreach (var c in (text ?? "").ToLowerInvariant())
        {
            if (_vocab.TryGetValue(c.ToString(), out var id))
            {
                ids.Add(BlankId);
                ids.Add(id);
            }
        }
        ids.Add(BlankId);
        return ids.ToArray();
    }
}