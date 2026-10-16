namespace InteractiveAI.Backend.Config;

public class MasterOptions
{
    public string Provider { get; set; } = "Local";
    public string BaseUrl { get; set; } = string.Empty;

    public string LookupPath { get; set; } = "/unit?nopol={nopol}";
    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeader { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
    public bool FallbackToLocal { get; set; } = true;
}
