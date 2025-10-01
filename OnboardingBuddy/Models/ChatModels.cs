namespace OnboardingBuddy.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class AIConfiguration
{
    public string AIService { get; set; } = "Acumatica"; // "OpenAI" or "Acumatica"
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-haiku-20240307";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public bool EnableStreaming { get; set; } = false;
    public string ApiVersion { get; set; } = "2024-10-01-preview";
    
    // Advanced Anthropic Features
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public List<string> StopSequences { get; set; } = new();
    
    // Tool Configuration
    public List<AnthropicTool> Tools { get; set; } = new();
    public string ToolChoice { get; set; } = "auto"; // auto, any, tool, none
    
    // Extended Thinking
    public ExtendedThinkingConfiguration? ExtendedThinking { get; set; }
    
    // Context Management
    public ContextManagementConfiguration? ContextManagement { get; set; }
    
    // OpenAI Responses API Configuration
    public bool UseResponsesApiStatefulMode { get; set; } = false;
    public bool StoreConversations { get; set; } = true;
    public bool BackgroundProcessing { get; set; } = false;
    public int? MaxToolCalls { get; set; }
    public bool ParallelToolCalls { get; set; } = true;
    public string? SafetyIdentifier { get; set; }
    public string ServiceTier { get; set; } = "auto"; // auto, default, flex, priority
    public string Truncation { get; set; } = "disabled"; // auto, disabled
    public int? TopLogprobs { get; set; }
    public List<string> BuiltInTools { get; set; } = new(); // web_search, file_search, code_interpreter
    public List<string> ResponseIncludes { get; set; } = new(); // Additional response data to include
    public Dictionary<string, string> Metadata { get; set; } = new();
    public bool UseStructuredOutput { get; set; } = false;
    public string? JsonSchema { get; set; }
    
    // Azure Key Vault Configuration
    public AzureKeyVaultConfiguration? AzureKeyVault { get; set; }
}

public class AzureKeyVaultConfiguration
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string KeyVaultName { get; set; } = string.Empty;
    public string ApiKeySecretName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

public class AnthropicTool
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? InputSchema { get; set; }
    public string? Description { get; set; }
}

public class ExtendedThinkingConfiguration
{
    public bool Enabled { get; set; } = false;
    public int? BudgetTokens { get; set; } = 8000;
}

public class ContextManagementConfiguration
{
    public bool ClearFunctionResults { get; set; } = true;
}