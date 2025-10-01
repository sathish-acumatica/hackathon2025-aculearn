using OnboardingBuddy.Models;
using System.Collections.Concurrent;

namespace OnboardingBuddy.Services;

public interface ISessionService
{
    Task<string> GetSessionIdAsync(string connectionId);
    Task<ConversationSession> GetOrCreateSessionAsync(string sessionId);
    Task UpdateSessionContextAsync(string sessionId, List<TrainingMaterial> materials, string userQuery);
    Task<bool> ShouldRefreshContextAsync(string sessionId, string userQuery);
    Task ClearSessionAsync(string sessionId);
    
    // New methods for session-based training context
    Task LoadTrainingContextForSessionAsync(string sessionId, string trainingContext);
    Task<string?> GetCachedTrainingContextAsync(string sessionId);
    Task AddConversationTurnAsync(string sessionId, string userMessage, string aiResponse);
    Task AddWelcomeMessageAsync(string sessionId, string welcomeMessage);
    Task<List<string>> GetConversationHistoryAsync(string sessionId, int maxTurns = 10);
    Task<bool> HasTrainingContextAsync(string sessionId);
    
    // Connection mapping for browser sessions
    Task MapConnectionToSessionAsync(string connectionId, string browserSessionId);
    Task<string?> GetSessionForConnectionAsync(string connectionId);
    Task RemoveConnectionMappingAsync(string connectionId);
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new(); // connectionId -> sessionId
    private readonly ILogger<SessionService> _logger;
    private const int SessionTimeoutMinutes = 60;

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
        
        // Start cleanup timer
        _ = Task.Run(CleanupExpiredSessions);
    }

    public async Task<string> GetSessionIdAsync(string connectionId)
    {
        // Check if this connection is mapped to a browser session
        if (_connectionToSession.TryGetValue(connectionId, out var sessionId))
        {
            return sessionId;
        }
        
        // Fallback to connectionId if no mapping exists
        return await Task.FromResult(connectionId);
    }

    public async Task MapConnectionToSessionAsync(string connectionId, string browserSessionId)
    {
        _connectionToSession[connectionId] = browserSessionId;
        _logger.LogInformation("Mapped connection {ConnectionId} to browser session {SessionId}", 
            connectionId, browserSessionId);
        await Task.CompletedTask;
    }

    public async Task<string?> GetSessionForConnectionAsync(string connectionId)
    {
        _connectionToSession.TryGetValue(connectionId, out var sessionId);
        return await Task.FromResult(sessionId);
    }

    public async Task RemoveConnectionMappingAsync(string connectionId)
    {
        if (_connectionToSession.TryRemove(connectionId, out var sessionId))
        {
            _logger.LogInformation("Removed connection mapping for {ConnectionId} (was mapped to session {SessionId})", 
                connectionId, sessionId);
        }
        await Task.CompletedTask;
    }

    public async Task<ConversationSession> GetOrCreateSessionAsync(string sessionId)
    {
        var session = _sessions.GetOrAdd(sessionId, id => new ConversationSession
        {
            SessionId = id,
            StartTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ConversationHistory = new List<ConversationTurn>(),
            LoadedMaterials = new List<int>(),
            CurrentTopics = new HashSet<string>()
        });

        session.LastActivity = DateTime.UtcNow;
        return await Task.FromResult(session);
    }

    public async Task UpdateSessionContextAsync(string sessionId, List<TrainingMaterial> materials, string userQuery)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ConversationHistory.Add(new ConversationTurn
            {
                UserQuery = userQuery,
                Timestamp = DateTime.UtcNow,
                MaterialsUsed = materials.Select(m => m.Id).ToList()
            });

            var newMaterialIds = materials.Select(m => m.Id).ToList();
            session.LoadedMaterials.AddRange(newMaterialIds);
            session.LoadedMaterials = session.LoadedMaterials.Distinct().ToList();

            var topics = ExtractTopics(userQuery);
            foreach (var topic in topics)
            {
                session.CurrentTopics.Add(topic);
            }

            foreach (var material in materials)
            {
                session.CurrentTopics.Add(material.Category.ToLower());
            }

            session.LastActivity = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }

    public async Task<bool> ShouldRefreshContextAsync(string sessionId, string userQuery)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return true;
        }

        if (!session.LoadedMaterials.Any())
        {
            return true;
        }

        var timeSinceLastActivity = DateTime.UtcNow - session.LastActivity;
        if (timeSinceLastActivity.TotalMinutes > 30)
        {
            return true;
        }

        return await Task.FromResult(false);
    }

    public async Task ClearSessionAsync(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _logger.LogInformation("Cleared session {SessionId}", sessionId);
        await Task.CompletedTask;
    }

    public async Task LoadTrainingContextForSessionAsync(string sessionId, string trainingContext)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.CachedTrainingContext = trainingContext;
            session.TrainingContextLoadedAt = DateTime.UtcNow;
            session.HasInitialTrainingContext = true;
            session.LastActivity = DateTime.UtcNow;
            
            _logger.LogInformation("Loaded training context for session {SessionId} (size: {Size} chars)", 
                sessionId, trainingContext?.Length ?? 0);
        }
        await Task.CompletedTask;
    }

    public async Task<string?> GetCachedTrainingContextAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.CachedTrainingContext;
        }
        return await Task.FromResult<string?>(null);
    }

    public async Task AddConversationTurnAsync(string sessionId, string userMessage, string aiResponse)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Add to conversation messages for Claude history
            session.ConversationMessages.Add($"Human: {userMessage}");
            session.ConversationMessages.Add($"Assistant: {aiResponse}");
            
            // Keep only last 20 messages (10 turns) to manage context size
            if (session.ConversationMessages.Count > 20)
            {
                session.ConversationMessages.RemoveRange(0, session.ConversationMessages.Count - 20);
            }
            
            // Add to conversation history
            session.ConversationHistory.Add(new ConversationTurn
            {
                UserQuery = userMessage,
                AIResponse = aiResponse,
                Timestamp = DateTime.UtcNow
            });
            
            session.LastActivity = DateTime.UtcNow;
        }
        await Task.CompletedTask;
    }

    public async Task AddWelcomeMessageAsync(string sessionId, string welcomeMessage)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Add welcome message only as Assistant message (no user message)
            session.ConversationMessages.Add($"Assistant: {welcomeMessage}");
            
            // Add to conversation history as a special welcome turn
            session.ConversationHistory.Add(new ConversationTurn
            {
                UserQuery = "", // Empty user query for welcome message
                AIResponse = welcomeMessage,
                Timestamp = DateTime.UtcNow
            });
            
            session.LastActivity = DateTime.UtcNow;
        }
        
        await Task.CompletedTask;
    }

    public async Task<List<string>> GetConversationHistoryAsync(string sessionId, int maxTurns = 10)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.ConversationMessages.TakeLast(maxTurns * 2).ToList();
        }
        return await Task.FromResult(new List<string>());
    }

    public async Task<bool> HasTrainingContextAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.HasInitialTrainingContext;
        }
        return await Task.FromResult(false);
    }

    private HashSet<string> ExtractTopics(string query)
    {
        var topics = new HashSet<string>();
        var words = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToArray();

        foreach (var word in words)
        {
            topics.Add(word);
        }

        return topics;
    }

    private async Task CleanupExpiredSessions()
    {
        while (true)
        {
            try
            {
                var expiredSessions = _sessions
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivity > TimeSpan.FromMinutes(SessionTimeoutMinutes))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var sessionId in expiredSessions)
                {
                    _sessions.TryRemove(sessionId, out _);
                    _logger.LogInformation("Removed expired session {SessionId}", sessionId);
                }

                await Task.Delay(TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }
    }
}

public class ConversationSession
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastActivity { get; set; }
    public List<ConversationTurn> ConversationHistory { get; set; } = new();
    public List<int> LoadedMaterials { get; set; } = new();
    public HashSet<string> CurrentTopics { get; set; } = new();
    
    // New session-based training context
    public string? CachedTrainingContext { get; set; }
    public DateTime? TrainingContextLoadedAt { get; set; }
    public bool HasInitialTrainingContext { get; set; } = false;
    public List<string> ConversationMessages { get; set; } = new(); // For Claude conversation history
    
    // OpenAI Responses API stateful support
    public string? OpenAIConversationId { get; set; }
    public string? LastOpenAIResponseId { get; set; }
    
    /// <summary>
    /// Determines if context should be refreshed for OpenAI Responses API
    /// Only needed for context switches or long conversations
    /// </summary>
    public bool ShouldRefreshContext()
    {
        // Refresh context if:
        // 1. Training context is stale (older than 30 minutes)
        // 2. Conversation is very long (more than 20 turns)
        // 3. No recent activity (more than 1 hour since last message)
        
        var now = DateTime.UtcNow;
        
        // Check if training context is stale
        if (TrainingContextLoadedAt.HasValue && 
            now.Subtract(TrainingContextLoadedAt.Value).TotalMinutes > 30)
        {
            return true;
        }
        
        // Check if conversation is getting too long
        if (ConversationHistory.Count > 20)
        {
            return true;
        }
        
        // Check if there's been a long gap in conversation
        if (ConversationHistory.Any() &&
            now.Subtract(ConversationHistory.Last().Timestamp).TotalHours > 1)
        {
            return true;
        }
        
        return false;
    }
}

public class ConversationTurn
{
    public string UserQuery { get; set; } = string.Empty;
    public string AIResponse { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<int> MaterialsUsed { get; set; } = new();
}