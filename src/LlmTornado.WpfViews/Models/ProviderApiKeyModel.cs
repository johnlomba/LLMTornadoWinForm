using LlmTornado.Code;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Represents a single provider's API key configuration.
/// </summary>
public class ProviderApiKeyModel
{
    /// <summary>
    /// The provider.
    /// </summary>
    public LLmProviders Provider { get; set; }
    
    /// <summary>
    /// The API key for this provider.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this provider is configured (has a non-empty API key).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    
    /// <summary>
    /// Display name for the provider.
    /// </summary>
    public string DisplayName => GetProviderDisplayName(Provider);
    
    /// <summary>
    /// Gets a user-friendly display name for a provider.
    /// </summary>
    public static string GetProviderDisplayName(LLmProviders provider)
    {
        return provider switch
        {
            LLmProviders.OpenAi => "OpenAI",
            LLmProviders.Anthropic => "Anthropic (Claude)",
            LLmProviders.AzureOpenAi => "Azure OpenAI",
            LLmProviders.Cohere => "Cohere",
            LLmProviders.Google => "Google (Gemini)",
            LLmProviders.Groq => "Groq",
            LLmProviders.DeepSeek => "DeepSeek",
            LLmProviders.Mistral => "Mistral",
            LLmProviders.XAi => "xAI (Grok)",
            LLmProviders.Perplexity => "Perplexity",
            LLmProviders.Voyage => "Voyage",
            LLmProviders.DeepInfra => "DeepInfra",
            LLmProviders.OpenRouter => "OpenRouter",
            LLmProviders.MoonshotAi => "Moonshot AI",
            LLmProviders.Zai => "ZAI",
            LLmProviders.Blablador => "Blablador",
            LLmProviders.Alibaba => "Alibaba",
            LLmProviders.Requesty => "Requesty",
            LLmProviders.Upstage => "Upstage",
            LLmProviders.Custom => "Custom/Self-hosted",
            _ => provider.ToString()
        };
    }
    
    /// <summary>
    /// Gets all available providers (excluding Unknown and Length).
    /// </summary>
    public static List<LLmProviders> GetAllProviders()
    {
        return Enum.GetValues<LLmProviders>()
            .Where(p => p != LLmProviders.Unknown && p != LLmProviders.Length)
            .ToList();
    }
}



