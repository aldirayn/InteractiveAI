namespace InteractiveAI.Backend.Config;

public class VoiceOptions
{

    public string DefaultVoice { get; set; } = "id-ID-GadisNeural";

    // Preferensi suara browser (dipakai jalur browser-TTS di frontend):
    // substring nama voice (case-insensitive), default suara Google Indonesia.
    // Kosongkan = otomatis voice `id` pertama. Contoh lain: "Microsoft Andika".
    public string BrowserVoice { get; set; } = "Google Bahasa Indonesia";

    public int MaxChars { get; set; } = 1000;
    public int DefaultRate { get; set; } = 0;

    public int CacheMinutes { get; set; } = 60;

    public string Provider { get; set; } = "ElevenLabs";

    public string VitsModelPath { get; set; } = "../../model/vits-indonesian.onnx";
    public int VitsSampleRate { get; set; } = 22050;
    public int VitsSpeakerId { get; set; } = 81;
    public float VitsNoiseScale { get; set; } = 0.33f;
    public float VitsLengthScale { get; set; } = 1.0f;
    public float VitsNoiseScaleW { get; set; } = 0.33f;

    public string MmsModelPath { get; set; } = "../../model/model.onnx";
    public string MmsVocabPath { get; set; } = "../../model/vocab.json";
    public int MmsSampleRate { get; set; } = 16000;

    public ElevenLabsOptions ElevenLabs { get; set; } = new();

    public VoiceFallbackOptions Fallback { get; set; } = new();
}


public class VoiceFallbackOptions
{

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "Mms";

    public int QuotaCooldownMinutes { get; set; } = 30;
}


public class ElevenLabsOptions
{

    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.elevenlabs.io";

    public string ModelId { get; set; } = "eleven_flash_v2_5";

    public string OutputFormat { get; set; } = "mp3_44100_128";

    public string DefaultVoiceId { get; set; } = "";

    public Dictionary<string, string> Voices { get; set; } = new();
    public double Stability { get; set; } = 0.4;
    public double SimilarityBoost { get; set; } = 0.8;
    public double Style { get; set; } = 0.35;
    public bool SpeakerBoost { get; set; } = true;

    public bool MapRateToSpeed { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
}
