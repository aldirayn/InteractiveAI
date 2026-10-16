namespace InteractiveAI.Backend.DTOs;

public class DetectionDto
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

public class DetectResponse
{
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string Model { get; set; } = string.Empty;
    public long InferenceMs { get; set; }
    public List<DetectionDto> Detections { get; set; } = new();

    public List<QrCodeDto> QrCodes { get; set; } = new();
}

public class QrCodeDto
{
    public string Format { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
