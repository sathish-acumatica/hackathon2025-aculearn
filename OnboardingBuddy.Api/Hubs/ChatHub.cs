using Microsoft.AspNetCore.SignalR;
using OnboardingBuddy.Api.Services;

namespace OnboardingBuddy.Api.Hubs;

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
        var sessionId = await _sessionService.GetSessionIdAsync(Context.ConnectionId);
        var session = await _sessionService.GetOrCreateSessionAsync(sessionId);
        
        _logger.LogInformation("Client connected: {ConnectionId}, Session: {SessionId}", Context.ConnectionId, sessionId);
        
        // Send welcome message
        try
        {
            var welcomeMessage = await _aiService.GenerateWelcomeMessageAsync(sessionId);
            await Clients.Caller.SendAsync("ReceiveMessage", welcomeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome message to {ConnectionId}", Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}