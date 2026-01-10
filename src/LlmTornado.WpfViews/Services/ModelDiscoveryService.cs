using LlmTornado.Code;
using LlmTornado.Models;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.Services;

/// <summary>
/// Service for discovering available models from providers.
/// Supports dynamic discovery from APIs and fallback to well-known model lists.
/// </summary>
/// <remarks>
/// This service provides:
/// - Dynamic model discovery via provider APIs
/// - Fallback to curated lists of well-known models
/// - Caching of discovered models
/// - Model metadata estimation (context size, capabilities)
/// </remarks>
public class ModelDiscoveryService
{
    private readonly Dictionary<LLmProviders, List<ModelOption>> _modelCache = new();
    private readonly Dictionary<LLmProviders, DateTime> _cacheTimestamps = new();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Event raised when model discovery completes.
    /// </summary>
    public event Action<LLmProviders, List<ModelOption>>? ModelsDiscovered;
    
    /// <summary>
    /// Event raised when model discovery fails.
    /// </summary>
    public event Action<LLmProviders, Exception>? DiscoveryFailed;
    
    /// <summary>
    /// Gets well-known models for a provider without making API calls.
    /// Useful as a fallback or when the API is unavailable.
    /// </summary>
    /// <param name="provider">The provider to get models for.</param>
    /// <returns>List of well-known models for the provider.</returns>
    public static List<ModelOption> GetWellKnownModels(LLmProviders provider)
    {
        return provider switch
        {
            LLmProviders.OpenAi => GetOpenAiModels(),
            LLmProviders.Anthropic => GetAnthropicModels(),
            LLmProviders.Google => GetGoogleModels(),
            LLmProviders.Cohere => GetCohereModels(),
            LLmProviders.Mistral => GetMistralModels(),
            LLmProviders.Groq => GetGroqModels(),
            _ => []
        };
    }
    
    private static List<ModelOption> GetOpenAiModels() =>
    [
        new() { Id = "o4-mini", DisplayName = "o4 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 200000 },
        new() { Id = "o3-mini", DisplayName = "o3 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 200000 },
        new() { Id = "gpt-4o", DisplayName = "GPT-4o", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
        new() { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
        new() { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
        new() { Id = "gpt-4", DisplayName = "GPT-4", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 8192 },
        new() { Id = "gpt-3.5-turbo", DisplayName = "GPT-3.5 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 16385 }
    ];
    
    private static List<ModelOption> GetAnthropicModels() =>
    [
        new() { Id = "claude-sonnet-4-20250514", DisplayName = "Claude 4 Sonnet", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 },
        new() { Id = "claude-3-5-sonnet-20241022", DisplayName = "Claude 3.5 Sonnet", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 },
        new() { Id = "claude-3-5-haiku-20241022", DisplayName = "Claude 3.5 Haiku", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 },
        new() { Id = "claude-3-opus-20240229", DisplayName = "Claude 3 Opus", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 },
        new() { Id = "claude-3-sonnet-20240229", DisplayName = "Claude 3 Sonnet", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 },
        new() { Id = "claude-3-haiku-20240307", DisplayName = "Claude 3 Haiku", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000 }
    ];
    
    private static List<ModelOption> GetGoogleModels() =>
    [
        new() { Id = "gemini-2.0-flash", DisplayName = "Gemini 2.0 Flash", ProviderEnum = LLmProviders.Google, MaxContextTokens = 1000000 },
        new() { Id = "gemini-1.5-pro", DisplayName = "Gemini 1.5 Pro", ProviderEnum = LLmProviders.Google, MaxContextTokens = 2000000 },
        new() { Id = "gemini-1.5-flash", DisplayName = "Gemini 1.5 Flash", ProviderEnum = LLmProviders.Google, MaxContextTokens = 1000000 },
        new() { Id = "gemini-1.0-pro", DisplayName = "Gemini 1.0 Pro", ProviderEnum = LLmProviders.Google, MaxContextTokens = 32768 }
    ];
    
    private static List<ModelOption> GetCohereModels() =>
    [
        new() { Id = "command-r-plus", DisplayName = "Command R+", ProviderEnum = LLmProviders.Cohere, MaxContextTokens = 128000 },
        new() { Id = "command-r", DisplayName = "Command R", ProviderEnum = LLmProviders.Cohere, MaxContextTokens = 128000 },
        new() { Id = "command", DisplayName = "Command", ProviderEnum = LLmProviders.Cohere, MaxContextTokens = 4096 }
    ];
    
    private static List<ModelOption> GetMistralModels() =>
    [
        new() { Id = "mistral-large-latest", DisplayName = "Mistral Large", ProviderEnum = LLmProviders.Mistral, MaxContextTokens = 128000 },
        new() { Id = "mistral-medium-latest", DisplayName = "Mistral Medium", ProviderEnum = LLmProviders.Mistral, MaxContextTokens = 32768 },
        new() { Id = "mistral-small-latest", DisplayName = "Mistral Small", ProviderEnum = LLmProviders.Mistral, MaxContextTokens = 32768 },
        new() { Id = "codestral-latest", DisplayName = "Codestral", ProviderEnum = LLmProviders.Mistral, MaxContextTokens = 32768 }
    ];
    
    private static List<ModelOption> GetGroqModels() =>
    [
        new() { Id = "llama-3.3-70b-versatile", DisplayName = "Llama 3.3 70B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 128000 },
        new() { Id = "llama-3.1-8b-instant", DisplayName = "Llama 3.1 8B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 128000 },
        new() { Id = "mixtral-8x7b-32768", DisplayName = "Mixtral 8x7B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 32768 },
        new() { Id = "gemma2-9b-it", DisplayName = "Gemma 2 9B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 8192 }
    ];
    
    /// <summary>
    /// Discovers models from a specific provider using the provided TornadoApi instance.
    /// Falls back to well-known models if the API call fails.
    /// </summary>
    public async Task<List<ModelOption>> DiscoverModelsAsync(TornadoApi api, LLmProviders provider, bool useCache = true)
    {
        // Check cache
        if (useCache && _modelCache.TryGetValue(provider, out var cached) && 
            _cacheTimestamps.TryGetValue(provider, out var timestamp) &&
            DateTime.UtcNow - timestamp < CacheExpiry)
        {
            return cached;
        }
        
        try
        {
            var retrievedModels = await api.Models.GetModels(provider);
            
            if (retrievedModels == null || retrievedModels.Count == 0)
            {
                // Fall back to well-known models
                var wellKnown = GetWellKnownModels(provider);
                CacheModels(provider, wellKnown);
                return wellKnown;
            }
            
            var modelOptions = new List<ModelOption>();
            
            foreach (var model in retrievedModels)
            {
                // Skip models that don't have an ID
                if (string.IsNullOrWhiteSpace(model.Id))
                {
                    continue;
                }
                
                var modelOption = new ModelOption
                {
                    Id = model.Id,
                    DisplayName = FormatModelDisplayName(model.Id, provider),
                    ProviderEnum = provider,
                    ApiName = model.Id,
                    IsCustom = false,
                    MaxContextTokens = EstimateMaxContextTokens(model.Id, provider)
                };
                
                modelOptions.Add(modelOption);
            }
            
            var result = modelOptions.OrderBy(m => m.DisplayName).ToList();
            CacheModels(provider, result);
            ModelsDiscovered?.Invoke(provider, result);
            return result;
        }
        catch (Exception ex)
        {
            DiscoveryFailed?.Invoke(provider, ex);
            
            // Return well-known models as fallback
            var fallback = GetWellKnownModels(provider);
            if (fallback.Count > 0)
            {
                CacheModels(provider, fallback);
                return fallback;
            }
            
            return [];
        }
    }
    
    private void CacheModels(LLmProviders provider, List<ModelOption> models)
    {
        _modelCache[provider] = models;
        _cacheTimestamps[provider] = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Clears the model cache for a specific provider or all providers.
    /// </summary>
    public void ClearCache(LLmProviders? provider = null)
    {
        if (provider.HasValue)
        {
            _modelCache.Remove(provider.Value);
            _cacheTimestamps.Remove(provider.Value);
        }
        else
        {
            _modelCache.Clear();
            _cacheTimestamps.Clear();
        }
    }
    
    /// <summary>
    /// Discovers models from all configured providers in the settings.
    /// </summary>
    public async Task<List<ModelOption>> DiscoverAllModelsAsync(SettingsModel settings)
    {
        var allModels = new List<ModelOption>();
        
        // Create a TornadoApi instance with all configured providers
        var authList = new List<ProviderAuthentication>();
        TornadoApi? api = null;
        
        foreach (var kvp in settings.ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                // Handle Azure specially
                if (kvp.Key == LLmProviders.AzureOpenAi)
                {
                    if (!string.IsNullOrWhiteSpace(settings.AzureEndpoint))
                    {
                        authList.Add(new ProviderAuthentication(kvp.Key, kvp.Value, settings.AzureOrganization));
                    }
                }
                else if (kvp.Key != LLmProviders.Custom)
                {
                    authList.Add(new ProviderAuthentication(kvp.Key, kvp.Value));
                }
            }
        }
        
        // Handle Custom provider separately (requires endpoint URL)
        if (settings.CustomEndpoints.TryGetValue(LLmProviders.Custom, out var customEndpoint) && 
            !string.IsNullOrWhiteSpace(customEndpoint))
        {
            if (Uri.TryCreate(customEndpoint, UriKind.Absolute, out var customUri))
            {
                var customApiKey = settings.ApiKeys.TryGetValue(LLmProviders.Custom, out var key) && !string.IsNullOrWhiteSpace(key) 
                    ? key 
                    : string.Empty;
                
                var customApi = new TornadoApi(customUri, customApiKey, LLmProviders.Custom);
                
                if (authList.Count > 0)
                {
                    api = new TornadoApi(authList);
                    // Merge custom API configuration
                    api.ApiUrlFormat = customApi.ApiUrlFormat;
                }
                else
                {
                    api = customApi;
                }
            }
        }
        else if (authList.Count > 0)
        {
            api = new TornadoApi(authList);
        }
        
        if (api == null)
        {
            return allModels;
        }
        
        // Discover models from each configured provider
        var discoveryTasks = new List<Task<List<ModelOption>>>();
        
        // Add providers from API keys
        foreach (var provider in settings.ApiKeys.Keys)
        {
            if (!string.IsNullOrWhiteSpace(settings.ApiKeys[provider]))
            {
                // Skip Azure if endpoint is not configured
                if (provider == LLmProviders.AzureOpenAi && string.IsNullOrWhiteSpace(settings.AzureEndpoint))
                {
                    continue;
                }
                
                // Skip Custom - handled separately
                if (provider == LLmProviders.Custom)
                {
                    continue;
                }
                
                discoveryTasks.Add(DiscoverModelsAsync(api, provider));
            }
        }
        
        // Add Custom provider if endpoint is configured
        if (settings.CustomEndpoints.TryGetValue(LLmProviders.Custom, out var customEndpointUrl) && 
            !string.IsNullOrWhiteSpace(customEndpointUrl))
        {
            discoveryTasks.Add(DiscoverModelsAsync(api, LLmProviders.Custom));
        }
        
        var results = await Task.WhenAll(discoveryTasks);
        
        foreach (var models in results)
        {
            allModels.AddRange(models);
        }
        
        return allModels;
    }
    
    /// <summary>
    /// Formats a model ID into a user-friendly display name.
    /// </summary>
    private static string FormatModelDisplayName(string modelId, LLmProviders provider)
    {
        // Remove common prefixes and format nicely
        var displayName = modelId;
        
        // Handle common patterns
        if (displayName.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
        {
            displayName = displayName.Replace("gpt-", "GPT-", StringComparison.OrdinalIgnoreCase);
        }
        else if (displayName.StartsWith("claude-", StringComparison.OrdinalIgnoreCase))
        {
            displayName = displayName.Replace("claude-", "Claude ", StringComparison.OrdinalIgnoreCase);
        }
        else if (displayName.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
        {
            displayName = displayName.Replace("gemini-", "Gemini ", StringComparison.OrdinalIgnoreCase);
        }
        
        // Capitalize first letter
        if (displayName.Length > 0)
        {
            displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);
        }
        
        return displayName;
    }
    
    /// <summary>
    /// Estimates maximum context tokens for a model based on common patterns.
    /// </summary>
    private static int EstimateMaxContextTokens(string modelId, LLmProviders provider)
    {
        // Default estimates based on provider and model patterns
        var lowerModelId = modelId.ToLowerInvariant();
        
        // OpenAI models
        if (provider == LLmProviders.OpenAi || provider == LLmProviders.AzureOpenAi)
        {
            if (lowerModelId.Contains("gpt-4o") || lowerModelId.Contains("o3") || lowerModelId.Contains("o4"))
            {
                return 128000;
            }
            if (lowerModelId.Contains("gpt-4"))
            {
                return 128000;
            }
            if (lowerModelId.Contains("gpt-3.5"))
            {
                return 16385;
            }
        }
        
        // Anthropic models
        if (provider == LLmProviders.Anthropic)
        {
            if (lowerModelId.Contains("claude-3-5") || lowerModelId.Contains("claude-4"))
            {
                return 200000;
            }
            if (lowerModelId.Contains("claude-3"))
            {
                return 200000;
            }
            return 100000;
        }
        
        // Google models
        if (provider == LLmProviders.Google)
        {
            if (lowerModelId.Contains("gemini-2.0") || lowerModelId.Contains("gemini-1.5-pro"))
            {
                return 2000000;
            }
            if (lowerModelId.Contains("gemini-1.5"))
            {
                return 1000000;
            }
            return 32768;
        }
        
        // Default for unknown models
        return 4096;
    }
}

