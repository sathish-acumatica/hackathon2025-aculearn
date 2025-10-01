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

            // Always read fresh training materials from database - no caching
            var (materials, context) = await _trainingService.GetSessionContextAsync(sessionId, message, _sessionService);
            await _sessionService.LoadTrainingContextForSessionAsync(sessionId, context);
            var trainingContext = context;
            
            _logger.LogInformation("Loaded fresh training context for session {SessionId} with {MaterialCount} materials from database", 
                sessionId, materials.Count);
            
            // Get conversation history
            var conversationHistory = await _sessionService.GetConversationHistoryAsync(sessionId);
            
            var requestPayload = await CreateRequestPayloadWithHistory(message, trainingContext, conversationHistory, sessionId);
            var response = await SendAIRequestAsync(requestPayload);
            
            var result = ExtractResponseText(response);
            
            // Store OpenAI conversation/response IDs if using Responses API stateful mode
            if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase) && 
                _config.ApiUrl.Contains("responses", StringComparison.OrdinalIgnoreCase) && 
                _config.UseResponsesApiStatefulMode)
            {
                await StoreOpenAIResponseIds(response, sessionId);
            }
            
            // Store the conversation turn
            await _sessionService.AddConversationTurnAsync(sessionId, message, result);
            
            _logger.LogInformation("Processed message for session {SessionId} with conversation history ({HistoryCount} messages)", 
                sessionId, conversationHistory.Count);
            
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

            // Always read fresh training materials from database - no caching
            var (materials, context) = await _trainingService.GetSessionContextAsync(sessionId, message, _sessionService);
            await _sessionService.LoadTrainingContextForSessionAsync(sessionId, context);
            var trainingContext = context;
            
            _logger.LogInformation("Loaded fresh training context for file processing session {SessionId} with {MaterialCount} materials from database", 
                sessionId, materials.Count);
            
            var fileContext = BuildFileContext(files ?? new List<FileUpload>());
            var enhancedContext = string.IsNullOrWhiteSpace(trainingContext) 
                ? fileContext 
                : $"{trainingContext}\n\nAttached Files:\n{fileContext}";
            
            // Get conversation history for files messages too
            var conversationHistory = await _sessionService.GetConversationHistoryAsync(sessionId);
            
            var requestPayload = CreateRequestPayloadWithHistoryAndFiles(message, enhancedContext ?? "", conversationHistory, files ?? new List<FileUpload>());
            var response = await SendAIRequestAsync(requestPayload);
            
            var result = ExtractResponseText(response);
            
            // Store the conversation turn
            await _sessionService.AddConversationTurnAsync(sessionId, message, result);
            
            _logger.LogInformation("Processed message with files for session {SessionId} with conversation history ({HistoryCount} messages)", 
                sessionId, conversationHistory.Count);
            
            return result;
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

            // Always read fresh training materials from database - no caching
            var (materials, context) = await _trainingService.GetSessionContextAsync(sessionId, string.Empty, _sessionService);
            await _sessionService.LoadTrainingContextForSessionAsync(sessionId, context);
            var trainingContext = context;
            
            _logger.LogInformation("Loaded fresh training context for welcome generation in session {SessionId} with {MaterialCount} materials from database", 
                sessionId, materials.Count);

            // If no training materials available, fall back to hardcoded welcome
            if (string.IsNullOrWhiteSpace(trainingContext))
            {
                _logger.LogInformation("No training materials available for session {SessionId}, using default welcome", sessionId);
                return GetDefaultWelcomeMessage();
            }

            // Use training materials to generate welcome message
            var systemPrompt = BuildSystemPrompt(trainingContext);
            var welcomeDirective = "Generate a brief, friendly welcome message for a new employee using the available training materials. Ask about their role and provide relevant first steps.";

            object requestPayload;
            
            if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                // Check if using Responses API
                if (_config.ApiUrl.Contains("responses", StringComparison.OrdinalIgnoreCase))
                {
                    requestPayload = new
                    {
                        model = _config.Model,
                        max_output_tokens = Math.Min(_config.MaxTokens, 500),
                        temperature = _config.Temperature,
                        instructions = systemPrompt,
                        input = welcomeDirective
                    };
                }
                else
                {
                    // OpenAI Chat Completions API format
                    requestPayload = new
                    {
                        model = _config.Model,
                        max_tokens = Math.Min(_config.MaxTokens, 500),
                        temperature = _config.Temperature,
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = welcomeDirective }
                        }
                    };
                }
            }
            else
            {
                // Claude/Anthropic format
                requestPayload = new
                {
                    model = _config.Model,
                    max_tokens = Math.Min(_config.MaxTokens, 500),
                    temperature = _config.Temperature,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = welcomeDirective }
                    }
                };
            }

            var response = await SendAIRequestAsync(requestPayload);
            var result = ExtractResponseText(response);
            
            _logger.LogInformation("Generated welcome message using training materials for session {SessionId}", sessionId);
            return result;
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
        
        // Build message content with images if present
        var messageContent = BuildMessageContent(message, files);
        
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            // OpenAI format
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = messageContent }
            };
            
            return new
            {
                model = _config.Model,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                messages = messages.ToArray()
            };
        }
        else
        {
            // Claude/Anthropic format
            return new
            {
                model = _config.Model,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = messageContent }
                }
            };
        }
    }

    private object BuildMessageContent(string message, List<FileUpload>? files)
    {
        var contentParts = new List<object>();
        
        // Add text message
        if (!string.IsNullOrWhiteSpace(message))
        {
            contentParts.Add(new { type = "text", text = message });
        }
        
        // Add images
        if (files != null)
        {
            foreach (var file in files)
            {
                if (IsImageFile(file.ContentType))
                {
                    var base64Data = Convert.ToBase64String(file.FileContent);
                    
                    if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
                    {
                        // OpenAI format for images
                        contentParts.Add(new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{file.ContentType};base64,{base64Data}"
                            }
                        });
                    }
                    else
                    {
                        // Claude/Anthropic format for images
                        contentParts.Add(new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = file.ContentType,
                                data = base64Data
                            }
                        });
                    }
                }
            }
        }
        
        // If only text, return string. If mixed content, return array
        if (contentParts.Count == 1 && contentParts[0] is { } textPart && 
            textPart.GetType().GetProperty("type")?.GetValue(textPart)?.ToString() == "text")
        {
            return textPart.GetType().GetProperty("text")?.GetValue(textPart)?.ToString() ?? message;
        }
        
        return contentParts.ToArray();
    }
    
    private bool IsImageFile(string contentType)
    {
        return contentType?.StartsWith("image/") == true;
    }

    private async Task<object> CreateRequestPayloadWithHistory(string message, string context, List<string> conversationHistory, string sessionId)
    {
        var systemPrompt = BuildSystemPrompt(context);
        
        // Build messages array with conversation history
        var messages = new List<object>();
        
        // Add system message for OpenAI and Ollama
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }
        
        // Add conversation history
        for (int i = 0; i < conversationHistory.Count; i += 2)
        {
            if (i + 1 < conversationHistory.Count)
            {
                // Extract actual content without "Human:" and "Assistant:" prefixes
                var userMsg = conversationHistory[i].StartsWith("Human: ") ? 
                    conversationHistory[i].Substring(7) : conversationHistory[i];
                var assistantMsg = conversationHistory[i + 1].StartsWith("Assistant: ") ? 
                    conversationHistory[i + 1].Substring(11) : conversationHistory[i + 1];
                    
                messages.Add(new { role = "user", content = userMsg });
                messages.Add(new { role = "assistant", content = assistantMsg });
            }
        }
        
        // Add current message
        messages.Add(new { role = "user", content = message });
        
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            // Check if using Responses API
            if (_config.ApiUrl.Contains("responses", StringComparison.OrdinalIgnoreCase))
            {
                // Check if stateful mode is enabled
                if (_config.UseResponsesApiStatefulMode)
                {
                    // OpenAI Responses API stateful format with full features
                    var session = await _sessionService.GetOrCreateSessionAsync(sessionId);
                    return await BuildFullResponsesApiPayload(message, systemPrompt, session);
                }
                else
                {
                    // OpenAI Responses API format - flatten to single input string (legacy mode)
                    var inputText = systemPrompt;
                    
                    // Add conversation history
                    for (int i = 0; i < conversationHistory.Count; i += 2)
                    {
                        if (i + 1 < conversationHistory.Count)
                        {
                            var userMsg = conversationHistory[i].StartsWith("Human: ") ? 
                                conversationHistory[i].Substring(7) : conversationHistory[i];
                            var assistantMsg = conversationHistory[i + 1].StartsWith("Assistant: ") ? 
                                conversationHistory[i + 1].Substring(11) : conversationHistory[i + 1];
                                
                            inputText += $"\n\nUser: {userMsg}\nAssistant: {assistantMsg}";
                        }
                    }
                    
                    // Add current message
                    inputText += $"\n\nUser: {message}\nAssistant:";
                    
                    return new
                    {
                        model = _config.Model,
                        max_output_tokens = _config.MaxTokens,
                        temperature = _config.Temperature,
                        input = inputText,
                        store = _config.StoreConversations
                    };
                }
            }
            else
            {
                // OpenAI Chat Completions API format
                return new
                {
                    model = _config.Model,
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature,
                    messages = messages.ToArray()
                };
            }
        }
        else if (string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            // Ollama format (similar to OpenAI but with stream parameter)
            return new
            {
                model = _config.Model,
                messages = messages.ToArray(),
                stream = _config.EnableStreaming
            };
        }
        else
        {
            // Claude/Anthropic format with enhanced features
            var payload = new Dictionary<string, object>
            {
                ["model"] = _config.Model,
                ["max_tokens"] = _config.MaxTokens,
                ["temperature"] = _config.Temperature,
                ["system"] = systemPrompt,
                ["messages"] = messages.ToArray()
            };

            // Add advanced Anthropic features (respect extended thinking constraints)
            if (_config.TopP.HasValue)
            {
                // When thinking is enabled Anthropic only allows 0.95 <= top_p <= 1.0
                var topP = _config.TopP.Value;
                if (_config.ExtendedThinking?.Enabled == true)
                {
                    if (topP < 0.95) topP = 0.95;
                    if (topP > 1.0) topP = 1.0;
                }
                payload["top_p"] = topP;
            }
            
            // top_k must be omitted when extended thinking is enabled per Anthropic docs
            if (_config.TopK.HasValue && _config.ExtendedThinking?.Enabled != true)
                payload["top_k"] = _config.TopK.Value;
                
            if (_config.StopSequences?.Any() == true)
                payload["stop_sequences"] = _config.StopSequences.ToArray();
                
            if (_config.EnableStreaming)
                payload["stream"] = true;
                
            // Add tools if configured
            if (_config.Tools?.Any() == true)
            {
                var tools = _config.Tools.Select(tool => new Dictionary<string, object>
                {
                    ["name"] = tool.Name,
                    ["type"] = tool.Type
                }).ToArray();
                
                payload["tools"] = tools;
                payload["tool_choice"] = new { type = _config.ToolChoice };
            }
            
            // Add extended thinking
            if (_config.ExtendedThinking?.Enabled == true)
            {
                payload["thinking"] = new { 
                    type = "enabled",
                    budget_tokens = _config.ExtendedThinking.BudgetTokens ?? 8000
                };
            }

            return payload;
        }
    }

    private object CreateRequestPayloadWithHistoryAndFiles(string message, string context, List<string> conversationHistory, List<FileUpload> files)
    {
        var systemPrompt = BuildSystemPrompt(context);
        
        // Build messages array with conversation history
        var messages = new List<object>();
        
        // Add system message for OpenAI and Ollama
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }
        
        // Add conversation history
        for (int i = 0; i < conversationHistory.Count; i += 2)
        {
            if (i + 1 < conversationHistory.Count)
            {
                // Extract actual content without "Human:" and "Assistant:" prefixes
                var userMsg = conversationHistory[i].StartsWith("Human: ") ? 
                    conversationHistory[i].Substring(7) : conversationHistory[i];
                var assistantMsg = conversationHistory[i + 1].StartsWith("Assistant: ") ? 
                    conversationHistory[i + 1].Substring(11) : conversationHistory[i + 1];
                    
                messages.Add(new { role = "user", content = userMsg });
                messages.Add(new { role = "assistant", content = assistantMsg });
            }
        }
        
        // Add current message with files support
        var messageContent = BuildMessageContent(message, files);
        messages.Add(new { role = "user", content = messageContent });
        
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            // Check if using Responses API
            if (_config.ApiUrl.Contains("responses", StringComparison.OrdinalIgnoreCase))
            {
                // OpenAI Responses API format - flatten to single input string
                // Note: Responses API may not support image inputs in the same way as Chat Completions
                var inputText = systemPrompt;
                
                // Add conversation history
                for (int i = 0; i < conversationHistory.Count; i += 2)
                {
                    if (i + 1 < conversationHistory.Count)
                    {
                        var userMsg = conversationHistory[i].StartsWith("Human: ") ? 
                            conversationHistory[i].Substring(7) : conversationHistory[i];
                        var assistantMsg = conversationHistory[i + 1].StartsWith("Assistant: ") ? 
                            conversationHistory[i + 1].Substring(11) : conversationHistory[i + 1];
                            
                        inputText += $"\n\nUser: {userMsg}\nAssistant: {assistantMsg}";
                    }
                }
                
                // Add current message (flatten content to text only for now)
                var textContent = message;
                if (files?.Any() == true)
                {
                    var fileContext = BuildFileContext(files);
                    textContent += $"\n\nAttached Files:\n{fileContext}";
                }
                inputText += $"\n\nUser: {textContent}\nAssistant:";
                
                return new
                {
                    model = _config.Model,
                    max_output_tokens = _config.MaxTokens,
                    temperature = _config.Temperature,
                    input = inputText
                };
            }
            else
            {
                // OpenAI Chat Completions API format
                return new
                {
                    model = _config.Model,
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature,
                    messages = messages.ToArray()
                };
            }
        }
        else if (string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            // Ollama format (similar to OpenAI but with stream parameter)
            return new
            {
                model = _config.Model,
                messages = messages.ToArray(),
                stream = _config.EnableStreaming
            };
        }
        else
        {
            // Claude/Anthropic format with enhanced features
            var payload = new Dictionary<string, object>
            {
                ["model"] = _config.Model,
                ["max_tokens"] = _config.MaxTokens,
                ["temperature"] = _config.Temperature,
                ["system"] = systemPrompt,
                ["messages"] = messages.ToArray()
            };

            // Add advanced Anthropic features (respect extended thinking constraints)
            if (_config.TopP.HasValue)
            {
                var topP = _config.TopP.Value;
                if (_config.ExtendedThinking?.Enabled == true)
                {
                    if (topP < 0.95) topP = 0.95;
                    if (topP > 1.0) topP = 1.0;
                }
                payload["top_p"] = topP;
            }
            
            if (_config.TopK.HasValue && _config.ExtendedThinking?.Enabled != true)
                payload["top_k"] = _config.TopK.Value;
                
            if (_config.StopSequences?.Any() == true)
                payload["stop_sequences"] = _config.StopSequences.ToArray();
                
            if (_config.EnableStreaming)
                payload["stream"] = true;
                
            // Add tools if configured
            if (_config.Tools?.Any() == true)
            {
                var tools = _config.Tools.Select(tool => new Dictionary<string, object>
                {
                    ["name"] = tool.Name,
                    ["type"] = tool.Type
                }).ToArray();
                
                payload["tools"] = tools;
                payload["tool_choice"] = new { type = _config.ToolChoice };
            }
            
            // Add extended thinking
            if (_config.ExtendedThinking?.Enabled == true)
            {
                payload["thinking"] = new { 
                    type = "enabled",
                    budget_tokens = _config.ExtendedThinking.BudgetTokens ?? 8000
                };
            }

            return payload;
        }
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
- Never use markdown syntax (no ## or ** or []() or ```) - always use HTML
- DO NOT wrap your response in code blocks or backticks
- DO NOT use ```html or ``` anywhere in your response
- Your entire response should be valid HTML that can be inserted directly into a web page
- Make responses visually appealing with proper HTML structure and formatting

CRITICAL KNOWLEDGE RESTRICTIONS:
- You MUST ONLY use information from the training materials provided in the context above
- DO NOT access any external knowledge, web searches, or information outside of the training materials
- If a user asks about something not covered in the training materials, politely explain that you can only help with topics covered in the provided onboarding materials
- Never make up information or provide answers from your general knowledge base
- Always base your responses strictly on the content from the training materials
- If the training materials don't contain sufficient information to answer a question, say so explicitly
- Examples of appropriate responses when information is not available:
  - 'I don't have information about that topic in the current training materials. Please check with your manager or HR for details.'
  - 'The training materials provided don't cover that specific question. You may want to consult the employee handbook or contact HR.'
  - 'That information isn't included in the onboarding materials I have access to. Please reach out to your supervisor for guidance.'";

        return basePrompt + formattingInstructions;
    }

    private async Task StoreOpenAIResponseIds(string jsonResponse, string sessionId)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;
            
            var session = await _sessionService.GetOrCreateSessionAsync(sessionId);
            
            // Extract response ID
            if (root.TryGetProperty("id", out var responseIdProperty))
            {
                session.LastOpenAIResponseId = responseIdProperty.GetString();
                _logger.LogInformation("Stored OpenAI response ID {ResponseId} for session {SessionId}", 
                    session.LastOpenAIResponseId, sessionId);
            }
            
            // Extract conversation ID if present
            if (root.TryGetProperty("conversation", out var conversationProperty) && 
                conversationProperty.TryGetProperty("id", out var conversationIdProperty))
            {
                session.OpenAIConversationId = conversationIdProperty.GetString();
                _logger.LogInformation("Stored OpenAI conversation ID {ConversationId} for session {SessionId}", 
                    session.OpenAIConversationId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract OpenAI response/conversation IDs from response");
        }
    }

    private async Task<object> BuildFullResponsesApiPayload(string message, string systemPrompt, ConversationSession session)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = _config.Model,
            ["max_output_tokens"] = _config.MaxTokens,
            ["temperature"] = _config.Temperature,
            ["store"] = _config.StoreConversations
        };

        // Input - support for simple string or complex array
        payload["input"] = message;

        // Conversation state management - OPTIMIZED for stateful API
        if (!string.IsNullOrEmpty(session.OpenAIConversationId))
        {
            // Subsequent messages - conversation ID maintains all context
            // NO need to send training data again!
            payload["conversation"] = session.OpenAIConversationId;
        }
        else if (!string.IsNullOrEmpty(session.LastOpenAIResponseId))
        {
            // Chain from previous response - context is maintained
            // NO need to send full training data again!
            payload["previous_response_id"] = session.LastOpenAIResponseId;
            
            // Only send instructions if this is a context switch or new topic
            // For normal conversation flow, previous context is preserved
            if (session.ShouldRefreshContext())
            {
                payload["instructions"] = "Continue helping with onboarding questions using previous context.";
            }
        }
        else
        {
            // First message ONLY (no conversation / previous response state):
            // Use dedicated 'instructions' field for system / training context so that
            // 'input' remains purely the latest user message. This avoids blending
            // persona + user content and aligns with OpenAI Responses API best practices.
            payload["instructions"] = systemPrompt;
            payload["input"] = message; // keep user content clean
        }

        // Advanced parameters
        if (_config.BackgroundProcessing)
            payload["background"] = true;

        if (_config.MaxToolCalls.HasValue)
            payload["max_tool_calls"] = _config.MaxToolCalls.Value;

        if (!_config.ParallelToolCalls)
            payload["parallel_tool_calls"] = false;

        // Generate session-based safety identifier for user tracking and abuse prevention
        var sessionHash = GenerateSessionHash(session.SessionId);
        payload["safety_identifier"] = $"acubuddy_session_{sessionHash}";

        if (_config.ServiceTier != "auto")
            payload["service_tier"] = _config.ServiceTier;

        if (_config.Truncation != "disabled")
            payload["truncation"] = _config.Truncation;

        if (_config.TopP.HasValue)
            payload["top_p"] = _config.TopP.Value;

        if (_config.TopLogprobs.HasValue)
            payload["top_logprobs"] = _config.TopLogprobs.Value;

        // Built-in tools
        if (_config.BuiltInTools.Any())
        {
            var tools = _config.BuiltInTools.Select(tool => new { type = tool }).ToArray();
            payload["tools"] = tools;
            payload["tool_choice"] = "auto";
        }

        // Response includes
        if (_config.ResponseIncludes.Any())
        {
            payload["include"] = _config.ResponseIncludes.ToArray();
        }

        // Metadata
        if (_config.Metadata.Any())
        {
            payload["metadata"] = _config.Metadata;
        }

        // Structured output
        if (_config.UseStructuredOutput && !string.IsNullOrEmpty(_config.JsonSchema))
        {
            payload["text"] = new
            {
                format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "response_schema",
                        schema = JsonSerializer.Deserialize<object>(_config.JsonSchema)
                    }
                }
            };
        }

        return payload;
    }

    private string GenerateSessionHash(string sessionId)
    {
        // Generate a SHA256 hash of the session ID for privacy-preserving user identification
        // This allows OpenAI to track usage patterns per user while protecting actual session IDs
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sessionId));
        
        // Take first 8 bytes and convert to hex for a shorter, privacy-preserving identifier
        var shortHash = Convert.ToHexString(hashBytes).Substring(0, 16).ToLowerInvariant();
        
        return shortHash;
    }

    private async Task<string> GetApiKeyAsync()
    {
        // Return cached runtime key if available
        if (!string.IsNullOrWhiteSpace(_runtimeApiKey))
        {
            _logger.LogInformation("Using cached API key (length: {Length})", _runtimeApiKey.Length);
            return _runtimeApiKey;
        }

        // Check AIService configuration to determine key source
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("AIService set to OpenAI, using ApiKey from appsettings.json");
            
            // For OpenAI, use ApiKey from configuration directly
            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                _runtimeApiKey = _config.ApiKey;
                _logger.LogInformation("Using OpenAI API key from configuration (length: {Length}, starts with: {Prefix})", 
                    _runtimeApiKey.Length, _runtimeApiKey.Substring(0, Math.Min(8, _runtimeApiKey.Length)));
                return _runtimeApiKey;
            }
            else
            {
                _logger.LogError("AIService is set to OpenAI but no ApiKey found in appsettings.json");
                return string.Empty;
            }
        }
        else if (string.Equals(_config.AIService, "Acumatica", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("AIService set to Acumatica, checking Azure Key Vault or configuration fallback");
            
            // For Acumatica, try Azure Key Vault first if ApiKey is empty
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                if (await _keyVaultService.IsConfiguredAsync())
                {
                    _logger.LogInformation("ApiKey is empty and Azure Key Vault is configured, attempting to retrieve API key...");
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
                    _logger.LogWarning("ApiKey is empty and Azure Key Vault is not configured");
                }
            }
            else
            {
                // If ApiKey is provided in config, use it directly
                _runtimeApiKey = _config.ApiKey;
                _logger.LogInformation("Using Acumatica API key from configuration (length: {Length}, starts with: {Prefix})", 
                    _runtimeApiKey.Length, _runtimeApiKey.Substring(0, Math.Min(8, _runtimeApiKey.Length)));
                return _runtimeApiKey;
            }
        }
        else if (string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("AIService set to Ollama, no API key required");
            // Ollama doesn't require an API key
            return "ollama-no-key-required";
        }
        else
        {
            _logger.LogWarning("Unknown AIService value: {AIService}. Expected 'OpenAI', 'Acumatica', or 'Ollama'", _config.AIService);
        }

        _logger.LogError("No API key available from any configured source");
        return string.Empty;
    }

    private async Task<string> SendAIRequestAsync(object payload)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey) && !string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No API key available for AI service");
        }

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        
        // Configure headers based on AI service type
        if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            // OpenAI API authentication
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _logger.LogInformation("Making OpenAI API request with API key length: {KeyLength}", apiKey.Length);
        }
        else if (string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            // Ollama API - no authentication required
            _logger.LogInformation("Making Ollama API request to local instance");
        }
        else if (string.Equals(_config.AIService, "Acumatica", StringComparison.OrdinalIgnoreCase))
        {
            // Azure/Anthropic API authentication
            var accessToken = await _keyVaultService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("No Azure access token available, using API key only");
            }
            
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                _logger.LogInformation("Using Azure access token for authorization");
            }
            
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("api-version", _config.ApiVersion);
            
            // Add Anthropic-specific headers
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            
            // Add beta features header if using advanced tools
            if (_config.Tools?.Any() == true)
            {
                var betaFeatures = new List<string>();
                
                // Check for specific tool types that require beta
                foreach (var tool in _config.Tools)
                {
                    if (tool.Type.Contains("web_search") || tool.Type.Contains("code_execution"))
                    {
                        betaFeatures.Add("tools-2024-05-16");
                        break;
                    }
                }
                
                if (betaFeatures.Any())
                {
                    _httpClient.DefaultRequestHeaders.Add("anthropic-beta", string.Join(",", betaFeatures));
                }
            }
            
            _logger.LogInformation("Making AI API request with access token: {HasToken}, subscription key length: {KeyLength}, tools enabled: {HasTools}", 
                !string.IsNullOrWhiteSpace(accessToken), apiKey.Length, _config.Tools?.Any() == true);
        }

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
            string content = "";
            
            // Handle OpenAI response format
            if (string.Equals(_config.AIService, "OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                // Check if using Responses API
                if (_config.ApiUrl.Contains("responses", StringComparison.OrdinalIgnoreCase))
                {
                    // OpenAI Responses API format
                    content = document.RootElement
                        .GetProperty("output")[0]
                        .GetProperty("content")[0]
                        .GetProperty("text")
                        .GetString() ?? "I apologize, but I couldn't generate a proper response.";
                }
                else
                {
                    // OpenAI Chat Completions API format
                    content = document.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "I apologize, but I couldn't generate a proper response.";
                }
            }
            // Handle Ollama response format (similar to OpenAI)
            else if (string.Equals(_config.AIService, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                content = document.RootElement
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "I apologize, but I couldn't generate a proper response.";
            }
            // Handle Claude/Anthropic response format
            else
            {
                content = document.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "I apologize, but I couldn't generate a proper response.";
            }
            
            // Clean up any markdown artifacts
            return CleanMarkdownArtifacts(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting response text from AI response");
            return "I encountered an issue processing the response. Please try again.";
        }
    }

    private string CleanMarkdownArtifacts(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        
        // Remove markdown code block markers
        content = System.Text.RegularExpressions.Regex.Replace(content, @"```html\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"```\s*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        content = System.Text.RegularExpressions.Regex.Replace(content, @"^```\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        
        // Remove any remaining triple backticks
        content = content.Replace("```", "");
        
        // Trim whitespace
        content = content.Trim();
        
        return content;
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