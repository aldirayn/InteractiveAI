using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InteractiveAI.Backend.DTOs;


public class ChatRequest
{
    public Guid? SessionId { get; set; }
    public string? Model { get; set; }
    [Required, MinLength(1)]
    public string Message { get; set; } = string.Empty;
    [Range(0, 2)]
    public double Temperature { get; set; } = 0.7;

    public string? SystemPrompt { get; set; }
}

public class ChatResponse
{
    public Guid SessionId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class MessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}


public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
    [JsonPropertyName("options")]
    public OllamaChatOptions Options { get; set; } = new();
}

public class OllamaChatOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
}

public class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; } = new();
}

public class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaTagModel> Models { get; set; } = new();
}

public class OllamaTagModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
}
