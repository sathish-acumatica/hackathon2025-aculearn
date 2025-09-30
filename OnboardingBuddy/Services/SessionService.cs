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
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
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
        return await Task.FromResult(connectionId);
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
}

public class ConversationTurn
{
    public string UserQuery { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<int> MaterialsUsed { get; set; } = new();
}