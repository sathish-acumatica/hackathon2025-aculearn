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
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-haiku-20240307";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public string ApiVersion { get; set; } = "2024-10-01-preview";
    
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