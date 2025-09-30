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
    private readonly IAzureKeyVaultService _keyVaultService;
    private string? _runtimeApiKey;

    public AIService(HttpClient httpClient, IOptions<AIConfiguration> config, ILogger<AIService> logger, 
        ITrainingMaterialService trainingService, ISessionService sessionService, IAzureKeyVaultService keyVaultService)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        _trainingService = trainingService;
        _sessionService = sessionService;
        _keyVaultService = keyVaultService;
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
            
            var requestPayload = CreateRequestPayload(message, trainingContext, null);
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
            
            var requestPayload = CreateRequestPayload(message, enhancedContext ?? "", files ?? new List<FileUpload>());
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

            if (IsContentLimitError(ex))
            {
                return CreateContentLimitMessage();
            }
            
            return await GetFallbackResponseAsync(message);
        }
    }

    public async Task<string> GenerateWelcomeMessageAsync(string sessionId)
    {
        try
        {
            // If AI is not configured, return the default welcome immediately
            if (string.IsNullOrWhiteSpace(_config.ApiUrl))
            {
                return GetDefaultWelcomeMessage();
            }

            // Use a simplified prompt to avoid content limits
            var welcomePrompt = @"Generate a brief, friendly welcome message for a new employee. 
                Ask about their role and provide a clear first step for their onboarding.
                Keep it concise and professional.";

            // Use a simpler request with minimal context to avoid content limits
            var requestPayload = new
            {
                model = _config.Model,
                max_tokens = Math.Min(_config.MaxTokens, 500), // Limit tokens for welcome
                temperature = _config.Temperature,
                system = "You are AcuBuddy, a helpful AI assistant for new employee onboarding. Be welcoming but concise.",
                messages = new[]
                {
                    new { role = "user", content = welcomePrompt }
                }
            };

            var response = await SendAIRequestAsync(requestPayload);
            return ExtractResponseText(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating welcome message for session {SessionId}, using default", sessionId);
            return GetDefaultWelcomeMessage();
        }
    }

    private object CreateRequestPayload(string message, string context, List<FileUpload>? files)
    {
        var systemPrompt = BuildSystemPrompt(context);
        
        return new
        {
            model = _config.Model,
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature,
            system = systemPrompt, // System prompt as top-level parameter
            messages = new[]
            {
                new { role = "user", content = message }
            }
        };
    }

    private string BuildSystemPrompt(string context)
    {
        // System prompt comes from TrainingMaterials with category "System Prompts"
        // The context already includes system prompts from the training materials
        var basePrompt = !string.IsNullOrWhiteSpace(context) ? context : 
               "You are AcuBuddy, an AI assistant helping new employees with their onboarding journey.";
        
        // Add explicit HTML formatting instructions (like the old successful version)
        var formattingInstructions = @"

IMPORTANT RESPONSE FORMATTING RULES:
- Always respond with well-formatted HTML that can be rendered directly in a web interface
- Use proper HTML tags: <h2>, <h3>, <p>, <ul>, <li>, <strong>, <em>, <a>, <br>, etc.
- For lists, use <ul> and <li> tags
- For emphasis, use <strong> for bold and <em> for italic
- For links, use <a href='url' target='_blank'>link text</a>
- For code or technical terms, use <code>term</code>
- For line breaks, use <br> tags
- Structure your response with clear headings using <h2> and <h3>
- Keep paragraphs in <p> tags
- Never use markdown syntax (no ## or ** or []()) - always use HTML
- Your entire response should be valid HTML that can be inserted directly into a web page
- Make responses visually appealing with proper HTML structure and formatting";

        return basePrompt + formattingInstructions;
    }

    private async Task<string> GetApiKeyAsync()
    {
        // Return cached runtime key if available
        if (!string.IsNullOrWhiteSpace(_runtimeApiKey))
        {
            _logger.LogInformation("Using cached API key (length: {Length})", _runtimeApiKey.Length);
            return _runtimeApiKey;
        }

        // Try to get from Azure Key Vault first
        if (await _keyVaultService.IsConfiguredAsync())
        {
            _logger.LogInformation("Azure Key Vault is configured, attempting to retrieve API key...");
            var keyVaultApiKey = await _keyVaultService.GetApiKeyAsync();
            if (!string.IsNullOrWhiteSpace(keyVaultApiKey))
            {
                _runtimeApiKey = keyVaultApiKey;
                _logger.LogInformation("Successfully retrieved API key from Azure Key Vault (length: {Length}, starts with: {Prefix})", 
                    _runtimeApiKey.Length, _runtimeApiKey.Substring(0, Math.Min(8, _runtimeApiKey.Length)));
                return _runtimeApiKey;
            }
            else
            {
                _logger.LogWarning("Azure Key Vault returned null or empty API key");
            }
        }
        else
        {
            _logger.LogInformation("Azure Key Vault is not configured, checking configuration fallback...");
        }

        // Fallback to configuration
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _runtimeApiKey = _config.ApiKey;
            _logger.LogInformation("Using API key from configuration (length: {Length}, starts with: {Prefix})", 
                _runtimeApiKey.Length, _runtimeApiKey.Substring(0, Math.Min(8, _runtimeApiKey.Length)));
            return _runtimeApiKey;
        }

        _logger.LogError("No API key available from Azure Key Vault or configuration");
        return string.Empty;
    }

    private async Task<string> SendAIRequestAsync(object payload)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("No API key available for AI service");
        }

        // Get Azure access token for Bearer authorization
        var accessToken = await _keyVaultService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("No Azure access token available, using API key only");
        }

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        
        // Add Bearer token if available (matching your PowerShell script)
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            _logger.LogInformation("Using Azure access token for authorization");
        }
        
        // Add API key as subscription key (matching your PowerShell script)
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("api-version", _config.ApiVersion);

        _logger.LogInformation("Making AI API request with access token: {HasToken}, subscription key length: {KeyLength}", 
            !string.IsNullOrWhiteSpace(accessToken), apiKey.Length);

        var response = await _httpClient.PostAsync(_config.ApiUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("AI API request failed: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
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
            <h3>🤖 AcuBuddy Assistant</h3>
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
               ex.Message.Contains("content") && ex.Message.Contains("limit") ||
               ex.Message.Contains("maximum context length");
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
            <h3>📝 Content Simplified</h3>
            <p>Your request contained quite a bit of content. Let me help you with a more focused approach.</p>
            <p><strong>For better results, try:</strong></p>
            <ul>
                <li>🎯 Asking about one specific topic</li>
                <li>📝 Breaking complex questions into parts</li>
                <li>❓ Being more specific about what you need</li>
            </ul>
            <p>What specific aspect would you like to explore first? 🚀</p>
        </div>";
    }

    private string GetDefaultWelcomeMessage()
    {
        return @"<div class='welcome-message'>
            <h2>🎉 Welcome to your new role!</h2>
            <p>I'm AcuBuddy, your AI onboarding assistant. I'm here to guide you through your first steps and help you succeed in your new position!</p>
            
            <p><strong>Let's get started! 🚀</strong></p>
            
            <p>First, I'd love to know: <strong>What role will you be working in?</strong> This helps me provide personalized guidance for your department.</p>
            
            <p><strong>Your first step:</strong> Complete IT setup and security training. Most new hires finish this within their first day.</p>
            
            <p>I'll check back with you regularly to help with your progress. Feel free to ask me questions anytime!</p>
            
            <p><em>What questions do you have to get started? 💬</em></p>
        </div>";
    }
}