using CommunityToolkit.Mvvm.ComponentModel;
using LlmTornado.Code;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for managing a single provider's API key.
/// </summary>
public partial class ProviderApiKeyViewModel : ObservableObject
{
    private readonly LLmProviders _provider;
    
    [ObservableProperty]
    private string _apiKey = string.Empty;
    
    [ObservableProperty]
    private bool _isExpanded;
    
    /// <summary>
    /// The provider this ViewModel represents.
    /// </summary>
    public LLmProviders Provider => _provider;
    
    /// <summary>
    /// Display name for the provider.
    /// </summary>
    public string DisplayName => ProviderApiKeyModel.GetProviderDisplayName(_provider);
    
    /// <summary>
    /// Whether this provider is configured (has a non-empty API key).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    
    /// <summary>
    /// Whether this is Azure (requires special handling for endpoint).
    /// </summary>
    public bool IsAzure => _provider == LLmProviders.AzureOpenAi;
    
    [ObservableProperty]
    private string _azureEndpoint = string.Empty;
    
    [ObservableProperty]
    private string _azureOrganization = string.Empty;
    
    public ProviderApiKeyViewModel(LLmProviders provider)
    {
        _provider = provider;
    }
    
    /// <summary>
    /// Updates the API key.
    /// </summary>
    public void SetApiKey(string apiKey)
    {
        ApiKey = apiKey ?? string.Empty;
    }
    
    /// <summary>
    /// Validates the configuration for this provider.
    /// </summary>
    public string? Validate()
    {
        if (IsAzure)
        {
            if (string.IsNullOrWhiteSpace(AzureEndpoint))
            {
                return "Azure endpoint is required.";
            }
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return "Azure API key is required.";
            }
        }
        else
        {
            // For non-Azure providers, API key is optional (they might be self-hosted)
            // But if provided, it should not be empty
            if (!string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Trim().Length == 0)
            {
                return $"{DisplayName} API key cannot be empty if provided.";
            }
        }
        
        return null;
    }
}

