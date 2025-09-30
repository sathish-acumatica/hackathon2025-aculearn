using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using OnboardingBuddy.Models;
using Microsoft.Extensions.Options;

namespace OnboardingBuddy.Services;

public interface IAzureKeyVaultService
{
    Task<string?> GetApiKeyAsync();
    Task<string?> GetAccessTokenAsync();
    Task<bool> IsConfiguredAsync();
}

public class AzureKeyVaultService : IAzureKeyVaultService
{
    private readonly AzureKeyVaultConfiguration _config;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private SecretClient? _secretClient;
    private TokenCredential? _credential;
    private string? _cachedApiKey;
    private string? _cachedAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public AzureKeyVaultService(IOptions<AIConfiguration> aiConfig, ILogger<AzureKeyVaultService> logger)
    {
        _config = aiConfig.Value.AzureKeyVault ?? new AzureKeyVaultConfiguration();
        _logger = logger;

        if (IsConfigured())
        {
            InitializeSecretClient();
        }
    }

    public Task<bool> IsConfiguredAsync()
    {
        return Task.FromResult(IsConfigured());
    }

    private bool IsConfigured()
    {
        return _config.Enabled &&
               !string.IsNullOrWhiteSpace(_config.TenantId) &&
               !string.IsNullOrWhiteSpace(_config.ClientId) &&
               !string.IsNullOrWhiteSpace(_config.ClientSecret) &&
               !string.IsNullOrWhiteSpace(_config.KeyVaultName) &&
               !string.IsNullOrWhiteSpace(_config.ApiKeySecretName);
    }

    private void InitializeSecretClient()
    {
        try
        {
            var keyVaultUrl = $"https://{_config.KeyVaultName}.vault.azure.net/";

            // use a service principal, this should be for local or on prem servers only
            if (!string.IsNullOrEmpty(_config.ClientSecret))
            {
                _credential = new ClientSecretCredential(
                    _config.TenantId,
                    _config.ClientId,
                    _config.ClientSecret);
            }
            else // use rbac managed identities
            {
                _credential = new DefaultAzureCredential();
            }

            _secretClient = new SecretClient(new Uri(keyVaultUrl), _credential);

            _logger.LogInformation("Azure Key Vault client initialized for vault: {KeyVaultName}", _config.KeyVaultName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Key Vault client");
            _secretClient = null;
            _credential = null;
        }
    }

    public async Task<string?> GetApiKeyAsync()
    {
        if (!IsConfigured() || _secretClient == null)
        {
            _logger.LogWarning("Azure Key Vault is not properly configured or client is not initialized. Configured: {IsConfigured}, Client: {HasClient}",
                IsConfigured(), _secretClient != null);
            return null;
        }

        // Return cached key if available
        if (!string.IsNullOrWhiteSpace(_cachedApiKey))
        {
            _logger.LogInformation("Returning cached API key from Azure Key Vault (length: {Length})", _cachedApiKey.Length);
            return _cachedApiKey;
        }

        try
        {
            _logger.LogInformation("Retrieving API key from Azure Key Vault. Vault: {KeyVaultName}, Secret: {SecretName}",
                _config.KeyVaultName, _config.ApiKeySecretName);

            var response = await _secretClient.GetSecretAsync(_config.ApiKeySecretName);
            _cachedApiKey = response.Value.Value;

            _logger.LogInformation("Successfully retrieved API key from Azure Key Vault (length: {Length}, starts with: {Prefix})",
                _cachedApiKey?.Length ?? 0,
                !string.IsNullOrEmpty(_cachedApiKey) ? _cachedApiKey.Substring(0, Math.Min(8, _cachedApiKey.Length)) : "null");
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API key from Azure Key Vault. Vault: {KeyVaultName}, Secret: {SecretName}, TenantId: {TenantId}, ClientId: {ClientId}",
                _config.KeyVaultName, _config.ApiKeySecretName, _config.TenantId, _config.ClientId);
            return null;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!IsConfigured() || _credential == null)
        {
            _logger.LogWarning("Azure credentials not configured for access token retrieval");
            return null;
        }

        // Return cached token if still valid
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            _logger.LogInformation("Using cached Azure access token");
            return _cachedAccessToken;
        }

        try
        {
            _logger.LogInformation("Retrieving Azure access token...");

            var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var tokenResult = await _credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

            _cachedAccessToken = tokenResult.Token;
            _tokenExpiry = tokenResult.ExpiresOn.DateTime.AddMinutes(-5); // Refresh 5 minutes before expiry

            _logger.LogInformation("Successfully retrieved Azure access token (expires: {Expiry})", _tokenExpiry);
            return _cachedAccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Azure access token");
            return null;
        }
    }
}