namespace InteractiveAI.Backend.Config;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "https://ollama.com";
    public string DefaultModel { get; set; } = "gemma3:27b";
    public int TimeoutSeconds { get; set; } = 120;

    public string ApiKey { get; set; } = string.Empty;
}
