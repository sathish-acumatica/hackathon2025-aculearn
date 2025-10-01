using Microsoft.AspNetCore.SignalR;
using OnboardingBuddy.Services;

namespace OnboardingBuddy.Hubs;

public class ChatHub : Hub
{
    private readonly IAIService _aiService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IAIService aiService, ISessionService sessionService, ILogger<ChatHub> logger)
    {
        _aiService = aiService;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task SendMessage(string message)
    {
        try
        {
            var sessionId = await _sessionService.GetSessionIdAsync(Context.ConnectionId);
            var response = await _aiService.ProcessSessionMessageAsync(message, sessionId);
            
            await Clients.Caller.SendAsync("ReceiveMessage", response);
            
            _logger.LogInformation("Processed message for session {SessionId}: {Message}", sessionId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("ReceiveMessage", "I'm sorry, I encountered an error processing your request. Please try again.");
        }
    }

    public async Task RegisterBrowserSession(string browserSessionId)
    {
        try
        {
            await _sessionService.MapConnectionToSessionAsync(Context.ConnectionId, browserSessionId);
            var session = await _sessionService.GetOrCreateSessionAsync(browserSessionId);
            
            _logger.LogInformation("Registered browser session {SessionId} for connection {ConnectionId}", 
                browserSessionId, Context.ConnectionId);
                
            // Send welcome message only for new sessions (no conversation history)
            if (!session.ConversationHistory.Any())
            {
                try
                {
                    var welcomeMessage = await _aiService.GenerateWelcomeMessageAsync(browserSessionId);
                    
                    // Add welcome message to conversation history (no user message)
                    await _sessionService.AddWelcomeMessageAsync(browserSessionId, welcomeMessage);
                    
                    _logger.LogInformation("Sent welcome message for new session {SessionId}", browserSessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending welcome message for session {SessionId}", browserSessionId);
                }
            }
            else
            {
                _logger.LogInformation("Existing session {SessionId} reconnected with {MessageCount} messages", 
                    browserSessionId, session.ConversationHistory.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering browser session for connection {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task GetConversationHistory()
    {
        try
        {
            var sessionId = await _sessionService.GetSessionForConnectionAsync(Context.ConnectionId);
            if (sessionId != null)
            {
                var session = await _sessionService.GetOrCreateSessionAsync(sessionId);
                var conversationHistory = new List<object>();
                
                foreach (var turn in session.ConversationHistory)
                {
                    // Only add user message if it's not a welcome message (empty UserQuery)
                    if (!string.IsNullOrEmpty(turn.UserQuery))
                    {
                        conversationHistory.Add(new { 
                            id = Guid.NewGuid().ToString(),
                            text = turn.UserQuery,
                            isUser = true,
                            timestamp = turn.Timestamp
                        });
                    }
                    
                    // Always add assistant message
                    conversationHistory.Add(new { 
                        id = Guid.NewGuid().ToString(),
                        text = turn.AIResponse,
                        isUser = false,
                        timestamp = turn.Timestamp
                    });
                }
                
                await Clients.Caller.SendAsync("ConversationHistory", conversationHistory);
                _logger.LogInformation("Sent conversation history for session {SessionId}: {MessageCount} messages", 
                    sessionId, conversationHistory.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for connection {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Note: We'll wait for the client to register their browser session ID
        // before sending welcome message via RegisterBrowserSession method
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        // Clean up connection mapping (but keep session data)
        await _sessionService.RemoveConnectionMappingAsync(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }
}