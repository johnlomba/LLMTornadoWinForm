using LlmTornado.Code;
using LlmTornado.Models;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.Services;

/// <summary>
/// Service for discovering available models from providers.
/// </summary>
public class ModelDiscoveryService
{
    /// <summary>
    /// Discovers models from a specific provider using the provided TornadoApi instance.
    /// </summary>
    public async Task<List<ModelOption>> DiscoverModelsAsync(TornadoApi api, LLmProviders provider)
    {
        try
        {
            var retrievedModels = await api.Models.GetModels(provider);
            
            if (retrievedModels == null || retrievedModels.Count == 0)
            {
                return new List<ModelOption>();
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
            
            return modelOptions.OrderBy(m => m.DisplayName).ToList();
        }
        catch (Exception)
        {
            // Return empty list on error - provider might not be available, network issue, etc.
            return new List<ModelOption>();
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
                    if (customApi.Authentications.TryGetValue(LLmProviders.Custom, out var customAuth))
                    {
                        api.Authentications.TryAdd(LLmProviders.Custom, customAuth);
                    }
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

