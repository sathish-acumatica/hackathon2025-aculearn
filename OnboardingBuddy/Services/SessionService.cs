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

    Task AddConversationTurnAsync(string sessionId, string userMessage, string aiResponse);
    Task AddWelcomeMessageAsync(string sessionId, string welcomeMessage);
    Task AddAssistantMessageAsync(string sessionId, string aiResponse, string turnType, Dictionary<string,string>? metadata = null);
    Task<List<string>> GetConversationHistoryAsync(string sessionId, int maxTurns = 10);
    Task<bool> HasTrainingContextAsync(string sessionId);
    Task<bool> HasWelcomeMessageAsync(string sessionId);
    Task<string?> GetExistingWelcomeMessageAsync(string sessionId);
    
    // Connection mapping for browser sessions
    Task MapConnectionToSessionAsync(string connectionId, string browserSessionId);
    Task<string?> GetSessionForConnectionAsync(string connectionId);
    Task RemoveConnectionMappingAsync(string connectionId);
    // Invalidate cached training context for all active sessions
    Task InvalidateAllTrainingContextsAsync();
    
    // Notification methods for training material updates
    Task<List<string>> GetActiveSessionIdsAsync();
    Task NotifySessionOfTrainingUpdateAsync(string sessionId, string updateMessage);
    Task BroadcastTrainingUpdateToActiveSessionsAsync(string updateMessage);
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new(); // connectionId -> sessionId
    private readonly ILogger<SessionService> _logger;
    private readonly INotificationService _notificationService;
    private const int SessionTimeoutMinutes = 60;

    public SessionService(ILogger<SessionService> logger, INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
        
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
            // No longer caching training context in memory - always read from DB
            session.TrainingContextLoadedAt = DateTime.UtcNow;
            session.HasInitialTrainingContext = true;
            session.LastActivity = DateTime.UtcNow;
            
            _logger.LogInformation("Training context marked as loaded for session {SessionId} (will read fresh from DB)", 
                sessionId);
        }
        await Task.CompletedTask;
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
                Timestamp = DateTime.UtcNow,
                TurnType = "conversation"
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
                Timestamp = DateTime.UtcNow,
                TurnType = "welcome",
                Metadata = new Dictionary<string,string>{{"welcome","true"}}
            });
            
            session.LastActivity = DateTime.UtcNow;
        }
        
        await Task.CompletedTask;
    }

    public async Task AddAssistantMessageAsync(string sessionId, string aiResponse, string turnType, Dictionary<string,string>? metadata = null)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ConversationMessages.Add($"Assistant: {aiResponse}");
            session.ConversationHistory.Add(new ConversationTurn
            {
                UserQuery = string.Empty,
                AIResponse = aiResponse,
                Timestamp = DateTime.UtcNow,
                TurnType = turnType,
                Metadata = metadata ?? new Dictionary<string,string>()
            });
            session.LastActivity = DateTime.UtcNow;
        }
        await Task.CompletedTask;
    }

    public async Task<bool> HasWelcomeMessageAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return session.ConversationHistory.Any(t => t.TurnType == "welcome" || (t.Metadata != null && t.Metadata.ContainsKey("welcome")));
        }
        return await Task.FromResult(false);
    }

    public async Task<string?> GetExistingWelcomeMessageAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var turn = session.ConversationHistory.FirstOrDefault(t => t.TurnType == "welcome" || (t.Metadata != null && t.Metadata.ContainsKey("welcome")));
            return await Task.FromResult(turn?.AIResponse);
        }
        return await Task.FromResult<string?>(null);
    }

    public async Task InvalidateAllTrainingContextsAsync()
    {
        var count = 0;
        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;
            // No longer caching - just reset training context flags to force fresh DB reads
            session.HasInitialTrainingContext = false;
            session.TrainingContextLoadedAt = null;
            session.LoadedMaterials.Clear();
            count++;
        }
        _logger.LogInformation("Invalidated training context flags for {SessionCount} active session(s) - will read fresh from DB", count);
        await Task.CompletedTask;
    }

    public async Task<List<string>> GetActiveSessionIdsAsync()
    {
        var activeSessionIds = new List<string>();
        var cutoff = DateTime.UtcNow.AddMinutes(-SessionTimeoutMinutes);
        
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.LastActivity > cutoff)
            {
                activeSessionIds.Add(kvp.Key);
            }
        }
        
        return await Task.FromResult(activeSessionIds);
    }

    public async Task NotifySessionOfTrainingUpdateAsync(string sessionId, string updateMessage)
    {
        // Training material notifications disabled - only cache invalidation occurs
        _logger.LogInformation("Training material update notification disabled for session {SessionId}", sessionId);
        await Task.CompletedTask;
    }

    public async Task BroadcastTrainingUpdateToActiveSessionsAsync(string updateMessage)
    {
        // Training material notifications disabled - only cache invalidation occurs
        _logger.LogInformation("Training material broadcast notification disabled");
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
    
    // Session-based training context - NO CACHING, always read from DB
    public DateTime? TrainingContextLoadedAt { get; set; }
    public bool HasInitialTrainingContext { get; set; } = false;
    public List<string> ConversationMessages { get; set; } = new(); // For Claude conversation history
    
    // OpenAI Responses API stateful support
    public string? OpenAIConversationId { get; set; }
    public string? LastOpenAIResponseId { get; set; }
    
    /// <summary>
    /// Determines if context should be refreshed for OpenAI Responses API
    /// Since we don't cache, we always have fresh context from DB
    /// </summary>
    public bool ShouldRefreshContext()
    {
        // Since we always read fresh from DB, only refresh for:
        // 1. Very long conversations (more than 20 turns)
        // 2. No recent activity (more than 1 hour since last message)
        
        var now = DateTime.UtcNow;
        
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
    public string TurnType { get; set; } = "conversation"; // welcome, followup, conversation
    public Dictionary<string,string> Metadata { get; set; } = new();
}