using OnboardingBuddy.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace OnboardingBuddy.Services;

public interface IAIService
{
    Task<string> ProcessMessageAsync(string message);
    Task<string> ProcessSessionMessageAsync(string message, string sessionId);
    Task<string> ProcessMessageWithFilesAsync(string message, string sessionId, List<FileUpload> files);
    Task<string> GenerateWelcomeMessageAsync(string sessionId);
}

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfiguration _config;
    private readonly ILogger<AIService> _logger;
    private readonly ITrainingMaterialService _trainingService;
    private readonly ISessionService _sessionService;

    public AIService(HttpClient httpClient, IOptions<AIConfiguration> config, ILogger<AIService> logger, 
        ITrainingMaterialService trainingService, ISessionService sessionService)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        _trainingService = trainingService;
        _sessionService = sessionService;
    }

    public async Task<string> ProcessMessageAsync(string message)
    {
        return await ProcessSessionMessageAsync(message, "default-session");
    }

    public async Task<string> ProcessSessionMessageAsync(string message, string sessionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.ApiUrl))
            {
                return await GetFallbackResponseAsync(message);
            }

            var (materials, trainingContext) = await _trainingService.GetSessionContextAsync(sessionId, message, _sessionService);
            
            var requestPayload = await CreateRequestPayload(message, trainingContext, null);
            var response = await SendAIRequestAsync(requestPayload);
            
            var result = ExtractResponseText(response);
            
            _logger.LogInformation("Processed message for session {SessionId} with {MaterialCount} materials", 
                sessionId, materials.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI request for session {SessionId}", sessionId);
            
            if (IsRateLimitError(ex))
            {
                return CreateRateLimitMessage();
            }

            if (IsContentLimitError(ex))
            {
                return CreateContentLimitMessage();
            }
            
            return await GetFallbackResponseAsync(message);
        }
    }

    public async Task<string> ProcessMessageWithFilesAsync(string message, string sessionId, List<FileUpload> files)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.ApiUrl))
            {
                return await GetFallbackResponseAsync(message);
            }

            var (materials, trainingContext) = await _trainingService.GetSessionContextAsync(sessionId, message, _sessionService);
            
            var fileContext = BuildFileContext(files ?? new List<FileUpload>());
            var enhancedContext = string.IsNullOrWhiteSpace(trainingContext) 
                ? fileContext 
                : $"{trainingContext}\n\nAttached Files:\n{fileContext}";
            
            var requestPayload = await CreateRequestPayload(message, enhancedContext ?? "", files ?? new List<FileUpload>());
            var response = await SendAIRequestAsync(requestPayload);
            
            return ExtractResponseText(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI request with files for session {SessionId}", sessionId);
            
            if (IsRateLimitError(ex))
            {
                return CreateRateLimitMessage();
            }
            
            return await GetFallbackResponseAsync(message);
        }
    }

    public async Task<string> GenerateWelcomeMessageAsync(string sessionId)
    {
        try
        {
            var welcomePrompt = @"
                Generate a personalized welcome message for a new employee starting their onboarding.
                Be enthusiastic, professional, and include next steps.
                Ask about their role and provide clear guidance for getting started.
            ";

            return await ProcessSessionMessageAsync(welcomePrompt, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating welcome message for session {SessionId}", sessionId);
            return GetDefaultWelcomeMessage();
        }
    }

    private async Task<object> CreateRequestPayload(string message, string context, List<FileUpload>? files)
    {
        var systemPrompt = await BuildSystemPrompt(context);
        
        return new
        {
            model = _config.Model,
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = message }
            }
        };
    }

    private async Task<string> BuildSystemPrompt(string context)
    {
        // System prompt comes from TrainingMaterials with category "System Prompts"
        // The context already includes system prompts from the training materials
        return !string.IsNullOrWhiteSpace(context) ? context : 
               "You are OnboardingBuddy, an AI assistant helping new employees with their onboarding journey.";
    }

    private async Task<string> SendAIRequestAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", _config.ApiVersion);

        var response = await _httpClient.PostAsync(_config.ApiUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"AI API request failed: {response.StatusCode} - {errorContent}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private string ExtractResponseText(string jsonResponse)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonResponse);
            var content = document.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();
            
            return content ?? "I apologize, but I couldn't generate a proper response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting response text from AI response");
            return "I encountered an issue processing the response. Please try again.";
        }
    }

    private string BuildFileContext(List<FileUpload> files)
    {
        if (!files.Any()) return string.Empty;

        var context = new StringBuilder();
        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file.ProcessedContent))
            {
                context.AppendLine($"File: {file.OriginalFileName}");
                context.AppendLine(file.ProcessedContent);
                context.AppendLine();
            }
        }
        
        return context.ToString();
    }

    private async Task<string> GetFallbackResponseAsync(string message)
    {
        await Task.Delay(100); // Simulate processing time
        
        return @"<div class='fallback-message'>
            <h3>🤖 OnboardingBuddy Assistant</h3>
            <p>I'm here to help with your onboarding! However, my AI capabilities are currently unavailable.</p>
            <p><strong>I can still help you with:</strong></p>
            <ul>
                <li>📋 Accessing training materials</li>
                <li>📁 Finding uploaded documents</li>
                <li>💬 Basic onboarding guidance</li>
            </ul>
            <p>For immediate assistance, please contact your manager or HR department.</p>
        </div>";
    }

    private bool IsRateLimitError(Exception ex)
    {
        return ex is HttpRequestException httpEx && 
               (httpEx.Message.Contains("TooManyRequests") || 
                httpEx.Message.Contains("429") || 
                httpEx.Message.Contains("rate_limit"));
    }

    private bool IsContentLimitError(Exception ex)
    {
        return ex.Message.Contains("token") || 
               (ex.Message.Contains("content") && ex.Message.Contains("limit"));
    }

    private string CreateRateLimitMessage()
    {
        return @"<div class='rate-limit-message'>
            <h3>⏱️ Rate Limit Reached</h3>
            <p>I'm receiving too many requests right now. Please wait a moment and try again.</p>
            <p>I'll be ready to help once the rate limit resets! 😊</p>
        </div>";
    }

    private string CreateContentLimitMessage()
    {
        return @"<div class='content-limit-message'>
            <h3>📚 Too Much Content</h3>
            <p>That's a lot of information! Let me help you break it down into smaller pieces.</p>
            <p><strong>Try asking me about:</strong></p>
            <ul>
                <li>🎯 One specific topic at a time</li>
                <li>📝 Particular features or processes</li>
                <li>❓ Specific questions you have</li>
            </ul>
            <p>What's the main thing you'd like to learn about first? 🚀</p>
        </div>";
    }

    private string GetDefaultWelcomeMessage()
    {
        return @"<div class='welcome-message'>
            <h2>🎉 Welcome to OnboardingBuddy!</h2>
            <p>I'm your dedicated AI onboarding assistant, and I'm excited to help you get started!</p>
            <p><strong>What can I help you with today?</strong></p>
            <ul>
                <li>🚀 Getting started with your new role</li>
                <li>📚 Finding training materials</li>
                <li>❓ Answering questions about company policies</li>
                <li>📋 Tracking your onboarding progress</li>
            </ul>
            <p>Let's make your onboarding journey smooth and successful! What would you like to know first?</p>
        </div>";
    }
}