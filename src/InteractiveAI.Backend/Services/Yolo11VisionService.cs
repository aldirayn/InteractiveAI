using System.Diagnostics;
using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace InteractiveAI.Backend.Services;

public interface IVisionService
{
    VisionInfo Info { get; }
    DetectResponse Detect(byte[] imageBytes, float? confidence, string[]? classes, bool decodeQr);
}

public record VisionInfo(string Model, int InputSize, int LabelCount, string[] DefaultClasses);

public class Yolo11VisionService : IVisionService, IDisposable
{

    public static readonly string[] CocoLabels = new[]
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck",
        "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench",
        "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra",
        "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove",
        "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup",
        "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
        "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse",
        "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
        "refrigerator", "book", "clock", "vase", "scissors", "teddy bear",
        "hair drier", "toothbrush"
    };

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly VisionOptions _opt;
    private readonly string _modelFile;

    public VisionInfo Info { get; }

    public Yolo11VisionService(IOptions<VisionOptions> opt, IWebHostEnvironment env, ILogger<Yolo11VisionService> log)
    {
        _opt = opt.Value;
        var path = Path.IsPathRooted(_opt.ModelPath)
            ? _opt.ModelPath
            : Path.Combine(env.ContentRootPath, _opt.ModelPath);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Model YOLO11 tidak ditemukan di {path}. Download yolo11n.onnx dari https://github.com/ultralytics/assets (tag v8.3.0) lalu simpan ke folder Models/.", path);
        _modelFile = Path.GetFileName(path);
        _session = new InferenceSession(path);
        _inputName = _session.InputMetadata.Keys.First();
        Info = new VisionInfo(_modelFile, _opt.InputSize, CocoLabels.Length, _opt.Classes);
        log.LogInformation("YOLO11 vision siap: {Model} (input {Input})", _modelFile, _inputName);
    }

    public DetectResponse Detect(byte[] imageBytes, float? confidence, string[]? classes, bool decodeQr)
    {
        var conf = confidence ?? _opt.Confidence;
        var raw = classes is { Length: > 0 } ? classes : _opt.Classes;
        if (raw.Length == 0) raw = new[] { "person" };
        var wanted = raw
            .Select(c => c.Trim().ToLowerInvariant())
            .ToHashSet();
        var allowedIdx = CocoLabels
            .Select((label, i) => (label, i))
            .Where(t => wanted.Contains(t.label))
            .Select(t => t.i)
            .ToHashSet();
        if (allowedIdx.Count == 0)
            throw new ArgumentException($"Kelas tidak dikenal. Pilihan: {string.Join(", ", CocoLabels)}");

        using var image = Image.Load<Rgb24>(imageBytes);
        int origW = image.Width, origH = image.Height;
        int size = _opt.InputSize;


        float scale = Math.Min((float)size / origW, (float)size / origH);
        int newW = Math.Max(1, (int)Math.Round(origW * scale));
        int newH = Math.Max(1, (int)Math.Round(origH * scale));
        int padX = (size - newW) / 2;
        int padY = (size - newH) / 2;
        using var lb = image.Clone(ctx =>
        {
            ctx.Resize(newW, newH);
            ctx.Pad(size, size, new Color(new Rgb24(114, 114, 114)));
        });

        var sw = Stopwatch.StartNew();
        var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });
        lb.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < size; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < size; x++)
                {
                    tensor[0, 0, y, x] = row[x].R / 255f;
                    tensor[0, 1, y, x] = row[x].G / 255f;
                    tensor[0, 2, y, x] = row[x].B / 255f;
                }
            }
        });

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
        var output = results.First().AsTensor<float>();
        int numBoxes = output.Dimensions[2];
        sw.Stop();

        var boxes = new List<DetectionDto>(64);
        for (int i = 0; i < numBoxes; i++)
        {
            int best = -1;
            float bestScore = 0;
            for (int c = 0; c < CocoLabels.Length; c++)
            {
                float s = output[0, 4 + c, i];
                if (s > bestScore) { bestScore = s; best = c; }
            }
            if (best < 0 || bestScore < conf || !allowedIdx.Contains(best)) continue;

            float cx = output[0, 0, i], cy = output[0, 1, i];
            float w = output[0, 2, i], h = output[0, 3, i];
            float x1 = Math.Clamp((cx - w / 2 - padX) / scale, 0, origW);
            float y1 = Math.Clamp((cy - h / 2 - padY) / scale, 0, origH);
            float x2 = Math.Clamp((cx + w / 2 - padX) / scale, 0, origW);
            float y2 = Math.Clamp((cy + h / 2 - padY) / scale, 0, origH);
            if (x2 <= x1 || y2 <= y1) continue;
            boxes.Add(new DetectionDto
            {
                Label = CocoLabels[best],
                Confidence = bestScore,
                X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1
            });
        }

        return new DetectResponse
        {
            ImageWidth = origW,
            ImageHeight = origH,
            Model = _modelFile,
            InferenceMs = sw.ElapsedMilliseconds,
            Detections = Nms(boxes, _opt.Iou),
            QrCodes = decodeQr ? DecodeQr(image, origW, origH) : new()
        };
    }


    private static List<QrCodeDto> DecodeQr(Image<Rgb24> image, int w, int h)
    {
        try
        {
            var rgb = new byte[w * h * 3];
            image.CopyPixelDataTo(rgb);
            var luma = new ZXing.RGBLuminanceSource(rgb, w, h);
            var bitmap = new ZXing.Common.HybridBinarizer(luma);
            var reader = new ZXing.MultiFormatReader();
            var hints = new Dictionary<ZXing.DecodeHintType, object>
            {
                [ZXing.DecodeHintType.TRY_HARDER] = true,
                [ZXing.DecodeHintType.POSSIBLE_FORMATS] = new List<ZXing.BarcodeFormat>
                    { ZXing.BarcodeFormat.QR_CODE }
            };
            var result = reader.decode(new ZXing.BinaryBitmap(bitmap), hints);
            if (result is null) return new();
            return new List<QrCodeDto>
            {
                new() { Format = result.BarcodeFormat.ToString(), Text = result.Text }
            };
        }
        catch { return new(); }
    }

    private static List<DetectionDto> Nms(List<DetectionDto> boxes, float iouThresh)
    {
        var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();
        var keep = new List<DetectionDto>(sorted.Count);
        var dead = new bool[sorted.Count];
        for (int i = 0; i < sorted.Count; i++)
        {
            if (dead[i]) continue;
            keep.Add(sorted[i]);
            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (dead[j]) continue;
                if (IoU(sorted[i], sorted[j]) > iouThresh) dead[j] = true;
            }
        }
        return keep;
    }

    private static float IoU(DetectionDto a, DetectionDto b)
    {
        float x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        if (inter <= 0) return 0;
        float union = a.Width * a.Height + b.Width * b.Height - inter;
        return union <= 0 ? 0 : inter / union;
    }

    public void Dispose() => _session.Dispose();
}
