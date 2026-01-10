using LlmTornado.Code;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Represents a single provider's API key configuration.
/// </summary>
public class ProviderApiKeyModel
{
    public LLmProviders Provider { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    public string DisplayName => GetProviderDisplayName(Provider);
    
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
    
    public static List<LLmProviders> GetAllProviders()
    {
        return Enum.GetValues<LLmProviders>()
            .Where(p => p != LLmProviders.Unknown && p != LLmProviders.Length)
            .ToList();
    }
}
