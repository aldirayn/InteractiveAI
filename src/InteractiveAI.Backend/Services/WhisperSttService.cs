using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Options;
using Whisper.net;

namespace InteractiveAI.Backend.Services;

public class WhisperSttService : IDisposable
{
    private readonly SttOptions _opt;
    private readonly string _modelPath;
    private readonly ILogger<WhisperSttService> _log;
    private WhisperFactory? _factory;
    private readonly object _gate = new();
    private bool _failed;

    public WhisperSttService(IOptions<SttOptions> opt, IWebHostEnvironment env, ILogger<WhisperSttService> log)
    {
        _opt = opt.Value;
        _log = log;
        var p = opt.Value.Whisper.ModelPath;
        _modelPath = Path.IsPathRooted(p) ? p : Path.Combine(env.ContentRootPath, p);
    }

    public bool EnsureLoaded()
    {
        lock (_gate)
        {
            if (_factory is not null) return true;
            if (_failed) return false;
            try
            {
                if (!File.Exists(_modelPath))
                {
                    _log.LogWarning("Whisper model tidak ada: {Path}", _modelPath);
                    _failed = true;
                    return false;
                }
                _factory = WhisperFactory.FromPath(_modelPath);
                _log.LogInformation("Whisper siap ({Model})", _modelPath);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Whisper init gagal");
                _failed = true;
                return false;
            }
        }
    }

    public async Task<string> TranscribeAsync(float[] pcm, CancellationToken ct)
    {
        if (!EnsureLoaded()) throw new InvalidOperationException("Model Whisper belum tersedia.");
        using var processor = _factory!.CreateBuilder()
            .WithLanguage(_opt.Whisper.Language)
            .WithPrompt(_opt.Whisper.Prompt)
            .Build();
        using var ms = new MemoryStream();
        WriteWav(ms, pcm);
        ms.Position = 0;
        var sb = new System.Text.StringBuilder();
        await foreach (var seg in processor.ProcessAsync(ms, ct))
        {
            ct.ThrowIfCancellationRequested();
            sb.Append(seg.Text);
        }
        return sb.ToString().Trim();
    }

    private static void WriteWav(Stream s, float[] pcm)
    {
        using var bw = new BinaryWriter(s, System.Text.Encoding.UTF8, leaveOpen: true);
        bw.Write("RIFF".ToCharArray()); bw.Write(36 + pcm.Length * 2);
        bw.Write("WAVE".ToCharArray()); bw.Write("fmt ".ToCharArray()); bw.Write(16);
        bw.Write((short)1); bw.Write((short)1); bw.Write(16000); bw.Write(16000 * 2);
        bw.Write((short)2); bw.Write((short)16);
        bw.Write("data".ToCharArray()); bw.Write(pcm.Length * 2);
        foreach (var v in pcm)
            bw.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _factory?.Dispose();
            _factory = null;
        }
    }
}
