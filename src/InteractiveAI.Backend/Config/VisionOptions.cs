namespace InteractiveAI.Backend.Config;

public class VisionOptions
{

    public string ModelPath { get; set; } = "Models/yolo11n-coco.onnx";
    public int InputSize { get; set; } = 640;
    public float Confidence { get; set; } = 0.35f;
    public float Iou { get; set; } = 0.45f;

    public string[] Classes { get; set; } = Array.Empty<string>();
}
