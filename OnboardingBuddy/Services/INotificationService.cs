using Microsoft.AspNetCore.SignalR;
using OnboardingBuddy.Hubs;

namespace OnboardingBuddy.Services;

public interface INotificationService
{
    Task NotifySessionUpdateAsync(string sessionId, string message);
    Task BroadcastToAllClientsAsync(string message);
}

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(IHubContext<ChatHub> hubContext, ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifySessionUpdateAsync(string sessionId, string message)
    {
        try
        {
            // For now, we broadcast to all clients and let them filter based on their session
            // A more sophisticated approach would maintain session-to-connection mappings
            await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", new
            {
                message,
                sessionId,
                type = "training_update",
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            _logger.LogInformation("Sent system notification for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to session {SessionId}", sessionId);
        }
    }

    public async Task BroadcastToAllClientsAsync(string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", new
            {
                message,
                type = "training_update_broadcast",
                timestamp = DateTime.UtcNow.ToString("O")
            });
            
            _logger.LogInformation("Broadcasted system notification to all connected clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting notification to all clients");
        }
    }
}