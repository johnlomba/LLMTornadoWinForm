using System.Text.Json.Serialization;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Application settings model.
/// </summary>
public class SettingsModel
{
    /// <summary>
    /// OpenAI API key.
    /// </summary>
    public string? OpenAiApiKey { get; set; }
    
    /// <summary>
    /// Azure OpenAI endpoint (if using Azure).
    /// </summary>
    public string? AzureEndpoint { get; set; }
    
    /// <summary>
    /// Azure OpenAI API key (if using Azure).
    /// </summary>
    public string? AzureApiKey { get; set; }
    
    /// <summary>
    /// Whether to use Azure OpenAI instead of OpenAI.
    /// </summary>
    public bool UseAzure { get; set; }
    
    /// <summary>
    /// Selected model ID.
    /// </summary>
    public string SelectedModelId { get; set; } = "gpt-4";
    
    /// <summary>
    /// Default system prompt template ID.
    /// </summary>
    public string DefaultPromptTemplateId { get; set; } = "default";
    
    /// <summary>
    /// Whether to enable streaming responses.
    /// </summary>
    public bool EnableStreaming { get; set; } = true;
    
    /// <summary>
    /// Maximum tokens for responses.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
    
    /// <summary>
    /// Temperature setting (0.0 - 2.0).
    /// </summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>
    /// Whether to save conversation history.
    /// </summary>
    public bool SaveConversationHistory { get; set; } = true;
    
    /// <summary>
    /// Theme preference (Dark/Light).
    /// </summary>
    public string Theme { get; set; } = "Dark";
}

/// <summary>
/// Available model options.
/// </summary>
public class ModelOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = "OpenAI";
    public int MaxContextTokens { get; set; }
    
    public static List<ModelOption> GetDefaultModels()
    {
        return
        [
            new ModelOption { Id = "gpt-4o", DisplayName = "GPT-4o", Provider = "OpenAI", MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Provider = "OpenAI", MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", Provider = "OpenAI", MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-3.5-turbo", DisplayName = "GPT-3.5 Turbo", Provider = "OpenAI", MaxContextTokens = 16385 },
            new ModelOption { Id = "o3-mini", DisplayName = "o3 Mini", Provider = "OpenAI", MaxContextTokens = 128000 },
            new ModelOption { Id = "o4-mini", DisplayName = "o4 Mini", Provider = "OpenAI", MaxContextTokens = 200000 }
        ];
    }
}

