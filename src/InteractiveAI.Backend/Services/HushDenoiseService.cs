using System.Reflection;
using System.Runtime.InteropServices;
using InteractiveAI.Backend.Config;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;

public class HushDenoiseService : IDisposable
{
    private static class WeyaNc
    {
        private const string Lib = "weya_nc";
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr weya_nc_model_load_from_path(string path);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void weya_nc_model_free(IntPtr model);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr weya_nc_session_create(IntPtr model, UIntPtr input_sr, float atten_lim_db);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void weya_nc_session_free(IntPtr session);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr weya_nc_get_frame_length(IntPtr session);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern float weya_nc_process_frame(IntPtr session, [In] float[] input, [Out] float[] output);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void weya_nc_reset(IntPtr session);
    }

    private readonly ILogger<HushDenoiseService> _log;
    private readonly string _dllPath;
    private readonly string _modelPath;
    private readonly float _attenLim;
    private IntPtr _model = IntPtr.Zero;
    private int _frameLen;
    private bool _resolverSet;

    public bool Available => _model != IntPtr.Zero;

    public HushDenoiseService(IOptions<SttOptions> opt, IWebHostEnvironment env, ILogger<HushDenoiseService> log)
    {
        _log = log;
        var o = opt.Value.Hush;
        _dllPath = Resolve(o.DllPath, env.ContentRootPath);
        _modelPath = Resolve(o.ModelPath, env.ContentRootPath);
        _attenLim = o.AttenLimDb > 0 ? o.AttenLimDb : 100f;
    }

    private static string Resolve(string path, string contentRoot) =>
        Path.IsPathRooted(path) ? path : Path.Combine(contentRoot, path);

    public bool EnsureLoaded()
    {
        if (_model != IntPtr.Zero) return true;
        try
        {
            if (!File.Exists(_dllPath))
            {
                _log.LogWarning("Hush DLL tidak ada: {Path} (taruh manual, lihat README)", _dllPath);
                return false;
            }
            if (!_resolverSet)
            {
                NativeLibrary.SetDllImportResolver(typeof(HushDenoiseService).Assembly,
                    (name, asm, path) => name == "weya_nc" ? NativeLibrary.Load(_dllPath) : IntPtr.Zero);
                _resolverSet = true;
            }
            if (!File.Exists(_modelPath))
            {
                _log.LogWarning("Hush model tidak ada: {Path}", _modelPath);
                return false;
            }
            _model = WeyaNc.weya_nc_model_load_from_path(_modelPath);
            if (_model == IntPtr.Zero)
            {
                _log.LogWarning("Hush model_load_from_path gagal: {Path}", _modelPath);
                return false;
            }

            var s = WeyaNc.weya_nc_session_create(_model, (UIntPtr)16000, _attenLim);
            if (s == IntPtr.Zero) { _model = IntPtr.Zero; return false; }
            _frameLen = (int)WeyaNc.weya_nc_get_frame_length(s);
            WeyaNc.weya_nc_session_free(s);
            _log.LogInformation("Hush siap (frame {Frame} @16kHz)", _frameLen);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Hush init gagal, STT jalan tanpa denoise");
            _model = IntPtr.Zero;
            return false;
        }
    }

    public float[] Denoise(float[] pcm)
    {
        if (!EnsureLoaded()) return pcm;
        var session = WeyaNc.weya_nc_session_create(_model, (UIntPtr)16000, _attenLim);
        if (session == IntPtr.Zero) return pcm;
        try
        {
            WeyaNc.weya_nc_reset(session);
            var out_ = new float[pcm.Length];
            var inf = new float[_frameLen];
            var outf = new float[_frameLen];
            int frames = (pcm.Length + _frameLen - 1) / _frameLen;
            for (int f = 0; f < frames; f++)
            {
                Array.Clear(inf, 0, inf.Length);
                int n = Math.Min(_frameLen, pcm.Length - f * _frameLen);
                Array.Copy(pcm, f * _frameLen, inf, 0, n);
                WeyaNc.weya_nc_process_frame(session, inf, outf);
                Array.Copy(outf, 0, out_, f * _frameLen, n);
            }
            return out_;
        }
        finally
        {
            WeyaNc.weya_nc_session_free(session);
        }
    }

    public void Dispose()
    {
        if (_model != IntPtr.Zero)
        {
            try { WeyaNc.weya_nc_model_free(_model); } catch {  }
            _model = IntPtr.Zero;
        }
    }
}
