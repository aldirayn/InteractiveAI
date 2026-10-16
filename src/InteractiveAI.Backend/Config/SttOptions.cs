namespace InteractiveAI.Backend.Config;

public class SttOptions
{
    public string Provider { get; set; } = "Browser";
    public HushOptions Hush { get; set; } = new();
    public WhisperOptions Whisper { get; set; } = new();
}

public class HushOptions
{
    public bool Enabled { get; set; } = true;
    public string DllPath { get; set; } = "../../model/weya_nc.dll";
    public string ModelPath { get; set; } = "../../model/hush_onnx.tar.gz";
    public float AttenLimDb { get; set; } = 100f;
}

public class WhisperOptions
{
    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = "../../model/ggml-base.bin";
    public string Language { get; set; } = "id";

    public string Prompt { get; set; } = "B 9002 UXU, work order, bengkel truk, loket WO, reefer, genset";
}
