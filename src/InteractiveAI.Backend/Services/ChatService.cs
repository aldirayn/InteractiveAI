using InteractiveAI.Backend.Config;
using InteractiveAI.Backend.Data;
using InteractiveAI.Backend.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InteractiveAI.Backend.Services;


public record ChatStreamStart(Guid SessionId, string Model, List<OllamaChatMessage> History);

public interface IChatService
{
    Task<ChatResponse> SendAsync(ChatRequest req, CancellationToken ct);
    Task<ChatStreamStart> StartStreamAsync(ChatRequest req, CancellationToken ct);
    IAsyncEnumerable<string> StreamTokensAsync(ChatStreamStart start, double temperature, CancellationToken ct = default);
    Task<ChatResponse> FinishStreamAsync(Guid sessionId, string fullText, CancellationToken ct);
    Task<ChatResponse> GreetAsync(string? model, string? systemPrompt, string greeting, CancellationToken ct);
    Task<List<SessionDto>> GetSessionsAsync(CancellationToken ct);
    Task<List<MessageDto>> GetHistoryAsync(Guid sessionId, CancellationToken ct);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken ct);
}

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IOllamaService _ollama;
    private readonly OllamaOptions _opt;
    private readonly ILogger<ChatService> _log;

    public ChatService(AppDbContext db, IOllamaService ollama, IOptions<OllamaOptions> opt, ILogger<ChatService> log)
    {
        _db = db;
        _ollama = ollama;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<ChatResponse> SendAsync(ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            throw new ArgumentException("Message tidak boleh kosong.");

        _log.LogDebug("Chat masuk sesi {Session} modelMinta {ModelMinta} suhu {Temp} {Len} karakter systemPrompt {HasSys}: {Message}",
            req.SessionId?.ToString() ?? "baru", req.Model ?? "", req.Temperature, req.Message.Length,
            string.IsNullOrWhiteSpace(req.SystemPrompt) ? "tidak" : "ada", req.Message);

        var model = string.IsNullOrWhiteSpace(req.Model) ? _opt.DefaultModel : req.Model.Trim();

        ChatSession session;
        if (req.SessionId is null)
        {
            session = new ChatSession
            {
                Model = model,
                Title = req.Message.Length > 60 ? req.Message[..60] : req.Message
            };
            _db.Sessions.Add(session);

            if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
                _db.Messages.Add(new ChatMessage
                {
                    SessionId = session.Id,
                    Role = "system",
                    Content = req.SystemPrompt.Trim()
                });
        }
        else
        {
            session = await _db.Sessions.Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == req.SessionId, ct)
                ?? throw new KeyNotFoundException("Session tidak ditemukan.");
            if (string.IsNullOrWhiteSpace(session.Model)) session.Model = model;
            else model = session.Model;
        }
        await _db.SaveChangesAsync(ct);

        var history = await _db.Messages
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.Id)
            .Select(m => new OllamaChatMessage { Role = m.Role, Content = m.Content })
            .ToListAsync(ct);

        history.Add(new OllamaChatMessage { Role = "user", Content = req.Message });

        string answer;
        try
        {
            answer = await _ollama.ChatAsync(model, history, req.Temperature, ct);
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(ex, "Ollama gagal sesi {SessionId} model {Model} suhu {Temp} riwayat {Count} pesan",
                session.Id, model, req.Temperature, history.Count);
            throw new InvalidOperationException(
                $"Gagal menghubungi Ollama di {_opt.BaseUrl}. Pastikan OLLAMA_API_KEY valid (buat di https://ollama.com/settings/keys) dan model `{model}` tersedia di akun Cloud kamu. Detail: {ex.Message}", ex);
        }

        _db.Messages.Add(new ChatMessage { SessionId = session.Id, Role = "user", Content = req.Message });
        _db.Messages.Add(new ChatMessage { SessionId = session.Id, Role = "assistant", Content = answer });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Chat sesi {SessionId} model {Model} user {UserLen} karakter assistant {AnswerLen} karakter",
            session.Id, model, req.Message.Length, answer.Length);
        _log.LogDebug("Chat sesi {SessionId} user: {UserMessage}", session.Id, req.Message);
        _log.LogDebug("Chat sesi {SessionId} assistant: {Answer}", session.Id, answer);

        return new ChatResponse
        {
            SessionId = session.Id,
            Model = model,
            Response = answer,
            CreatedAt = DateTime.UtcNow
        };
    }


    public async Task<ChatStreamStart> StartStreamAsync(ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            throw new ArgumentException("Message tidak boleh kosong.");

        var model = string.IsNullOrWhiteSpace(req.Model) ? _opt.DefaultModel : req.Model.Trim();

        ChatSession session;
        if (req.SessionId is null)
        {
            session = new ChatSession
            {
                Model = model,
                Title = req.Message.Length > 60 ? req.Message[..60] : req.Message
            };
            _db.Sessions.Add(session);

            if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
                _db.Messages.Add(new ChatMessage
                {
                    SessionId = session.Id,
                    Role = "system",
                    Content = req.SystemPrompt.Trim()
                });
        }
        else
        {
            session = await _db.Sessions.Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == req.SessionId, ct)
                ?? throw new KeyNotFoundException("Session tidak ditemukan.");
            if (string.IsNullOrWhiteSpace(session.Model)) session.Model = model;
            else model = session.Model;
        }

        var history = await _db.Messages
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.Id)
            .Select(m => new OllamaChatMessage { Role = m.Role, Content = m.Content })
            .ToListAsync(ct);

        history.Add(new OllamaChatMessage { Role = "user", Content = req.Message });


        _db.Messages.Add(new ChatMessage { SessionId = session.Id, Role = "user", Content = req.Message });
        await _db.SaveChangesAsync(ct);

        _log.LogDebug("Stream mulai sesi {SessionId} model {Model} riwayat {Count} pesan user: {UserMessage}",
            session.Id, model, history.Count, req.Message);

        return new ChatStreamStart(session.Id, model, history);
    }

    public async IAsyncEnumerable<string> StreamTokensAsync(ChatStreamStart start, double temperature,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var tok in _ollama.ChatStreamAsync(start.Model, start.History, temperature, ct))
            yield return tok;
    }

    public async Task<ChatResponse> FinishStreamAsync(Guid sessionId, string fullText, CancellationToken ct)
    {
        var text = (fullText ?? "").Trim();
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Respons kosong.");
        _db.Messages.Add(new ChatMessage { SessionId = sessionId, Role = "assistant", Content = text });
        await _db.SaveChangesAsync(ct);
        _log.LogDebug("Stream selesai sesi {SessionId} {Len} karakter assistant: {Answer}", sessionId, text.Length, text);
        var session = await _db.Sessions.FindAsync(new object[] { sessionId }, ct);
        return new ChatResponse
        {
            SessionId = sessionId,
            Model = session?.Model ?? string.Empty,
            Response = text,
            CreatedAt = DateTime.UtcNow
        };
    }


    public async Task<ChatResponse> GreetAsync(string? model, string? systemPrompt, string greeting, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(greeting))
            throw new ArgumentException("Greeting tidak boleh kosong.");
        model = string.IsNullOrWhiteSpace(model) ? _opt.DefaultModel : model.Trim();

        var session = new ChatSession
        {
            Model = model,
            Title = greeting.Length > 60 ? greeting[..60] : greeting
        };
        _db.Sessions.Add(session);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            _db.Messages.Add(new ChatMessage { SessionId = session.Id, Role = "system", Content = systemPrompt.Trim() });
        _db.Messages.Add(new ChatMessage { SessionId = session.Id, Role = "assistant", Content = greeting.Trim() });
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Sapa sesi baru {SessionId} model {Model}", session.Id, model);
        _log.LogDebug("Sapa sesi {SessionId} assistant: {Greeting}", session.Id, greeting.Trim());

        return new ChatResponse
        {
            SessionId = session.Id,
            Model = model,
            Response = greeting.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<List<SessionDto>> GetSessionsAsync(CancellationToken ct) =>
        await _db.Sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Model = s.Model,
                CreatedAt = s.CreatedAt,
                MessageCount = s.Messages.Count
            }).ToListAsync(ct);

    public async Task<List<MessageDto>> GetHistoryAsync(Guid sessionId, CancellationToken ct)
    {
        var exists = await _db.Sessions.AnyAsync(s => s.Id == sessionId, ct);
        if (!exists) throw new KeyNotFoundException("Session tidak ditemukan.");
        return await _db.Messages.Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Id)
            .Select(m => new MessageDto { Role = m.Role, Content = m.Content, CreatedAt = m.CreatedAt })
            .ToListAsync(ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var s = await _db.Sessions.FindAsync(new object[] { sessionId }, ct);
        if (s is null) throw new KeyNotFoundException("Session tidak ditemukan.");
        _db.Sessions.Remove(s);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Sesi {SessionId} dihapus", sessionId);
    }
}
