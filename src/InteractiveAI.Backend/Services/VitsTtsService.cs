using System.Security.Cryptography;
using System.Text;
using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InteractiveAI.Backend.Services;

public class VitsTtsService : IVoiceService
{
    private readonly VoiceOptions _opt;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VitsTtsService> _log;
    private readonly VitsTokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly string _outputName;

    private static readonly Dictionary<string, int> SpeakerIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gadis"] = 81,
        ["ardi"] = 80,
        ["wibowo"] = 82
    };

    public VitsTtsService(IOptions<VoiceOptions> opt, IMemoryCache cache, ILogger<VitsTtsService> log,
        IWebHostEnvironment env)
    {
        _opt = opt.Value;
        _cache = cache;
        _log = log;

        var modelPath = Resolve(_opt.VitsModelPath, env.ContentRootPath);
        if (!File.Exists(modelPath)) throw new InvalidOperationException($"Model VITS tidak ditemukan: {modelPath}");

        _tokenizer = new VitsTokenizer();
        _session = new InferenceSession(modelPath);
        _outputName = _session.OutputMetadata.Keys.FirstOrDefault() ?? "output";
    }

    public Task<byte[]> SpeakAsync(string text, string? voice, int? rate, CancellationToken ct)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("Teks kosong.");
        if (text.Length > _opt.MaxChars)
            throw new ArgumentException($"Teks maksimal {_opt.MaxChars} karakter.");

        var spoken = CleanForVits(text);
        var sid = ResolveSpeaker(voice);
        var key = $"vits:{sid}|{Hash(spoken)}";
        if (_cache.TryGetValue(key, out byte[]? hit) && hit is { Length: > 0 })
            return Task.FromResult(hit);

        return Task.Run(() =>
        {
            var ids = _tokenizer.Encode(spoken);
            var input = new DenseTensor<long>(ids, new[] { 1, ids.Length });
            var lengths = new DenseTensor<long>(new[] { (long)ids.Length }, new[] { 1 });
            var scales = new DenseTensor<float>(
                new[] { _opt.VitsNoiseScale, _opt.VitsLengthScale, _opt.VitsNoiseScaleW }, new[] { 3 });
            var speaker = new DenseTensor<long>(new[] { (long)sid }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", input),
                NamedOnnxValue.CreateFromTensor("input_lengths", lengths),
                NamedOnnxValue.CreateFromTensor("scales", scales),
                NamedOnnxValue.CreateFromTensor("sid", speaker)
            };

            using var results = _session.Run(inputs);
            var samples = results.First(o => o.Name == _outputName).AsTensor<float>().ToArray();
            if (samples.Length == 0) throw new InvalidOperationException("VITS mengembalikan audio kosong.");

            var wav = ConvertToWav(samples, _opt.VitsSampleRate);
            _cache.Set(key, wav, TimeSpan.FromMinutes(Math.Clamp(_opt.CacheMinutes, 1, 24 * 60)));
            return wav;
        }, ct);
    }

    public Task<List<string>> IndonesianVoicesAsync()
        => Task.FromResult(new List<string> { "id-ID-GadisNeural", "id-ID-ArdiNeural" });

    private int ResolveSpeaker(string? voice)
    {
        if (string.IsNullOrWhiteSpace(voice) || voice.Equals("browser", StringComparison.OrdinalIgnoreCase))
            return _opt.VitsSpeakerId;
        var key = voice.Trim();
        if (key.StartsWith("id-id-", StringComparison.OrdinalIgnoreCase)) key = key["id-id-".Length..];
        if (key.EndsWith("neural", StringComparison.OrdinalIgnoreCase)) key = key[..^"neural".Length];
        return SpeakerIds.TryGetValue(key, out var id) ? id : _opt.VitsSpeakerId;
    }

    private static string CleanForVits(string text)
    {
        var s = text
            .Replace("```", " ")
            .Replace("**", "")
            .Replace("__", "");
        return string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Resolve(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(contentRoot, path);
    }

    private static byte[] ConvertToWav(float[] samples, int sampleRate)
    {
        var dataLen = samples.Length * 2;
        var wav = new byte[44 + dataLen];

        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        WriteInt(wav, 4, 36 + dataLen);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        WriteInt(wav, 16, 16);
        WriteShort(wav, 20, 1);
        WriteShort(wav, 22, 1);
        WriteInt(wav, 24, sampleRate);
        WriteInt(wav, 28, sampleRate * 2);
        WriteShort(wav, 32, 2);
        WriteShort(wav, 34, 16);
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        WriteInt(wav, 40, dataLen);

        for (int i = 0; i < samples.Length; i++)
        {
            var s = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            wav[44 + i * 2] = (byte)(s & 0xFF);
            wav[45 + i * 2] = (byte)((s >> 8) & 0xFF);
        }
        return wav;
    }

    private static void WriteInt(byte[] b, int at, int v)
    {
        b[at] = (byte)(v & 0xFF);
        b[at + 1] = (byte)((v >> 8) & 0xFF);
        b[at + 2] = (byte)((v >> 16) & 0xFF);
        b[at + 3] = (byte)((v >> 24) & 0xFF);
    }

    private static void WriteShort(byte[] b, int at, short v)
    {
        b[at] = (byte)(v & 0xFF);
        b[at + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..16];
    }
}