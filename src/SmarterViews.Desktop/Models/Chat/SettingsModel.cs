using System.Text.Json;
using System.Text.Json.Serialization;
using LlmTornado.Code;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Application settings model for LLM configuration.
/// </summary>
public class SettingsModel
{
    /// <summary>
    /// API keys for each provider. Key is the provider enum, value is the API key.
    /// </summary>
    [JsonConverter(typeof(DictionaryJsonConverter<LLmProviders, string>))]
    public Dictionary<LLmProviders, string> ApiKeys { get; set; } = new();
    
    /// <summary>
    /// Custom endpoints for self-hosted providers (like Ollama, LM Studio).
    /// </summary>
    [JsonConverter(typeof(DictionaryJsonConverter<LLmProviders, string>))]
    public Dictionary<LLmProviders, string> CustomEndpoints { get; set; } = new();
    
    /// <summary>
    /// Azure OpenAI endpoint (if using Azure).
    /// </summary>
    public string? AzureEndpoint { get; set; }
    
    /// <summary>
    /// Azure organization key (if using Azure).
    /// </summary>
    public string? AzureOrganization { get; set; }
    
    /// <summary>
    /// Custom models defined by the user.
    /// </summary>
    public List<CustomModelOption> CustomModels { get; set; } = new();
    
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
    
    // Legacy properties for backward compatibility
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpenAiApiKey { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AzureApiKey { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseAzure { get; set; }
}

/// <summary>
/// Custom model option defined by the user.
/// </summary>
public class CustomModelOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public LLmProviders Provider { get; set; }
    public string? ApiName { get; set; }
    public int MaxContextTokens { get; set; } = 4096;
}

/// <summary>
/// JSON converter for Dictionary with enum keys.
/// </summary>
public class DictionaryJsonConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>> where TKey : struct, Enum
{
    public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var dictionary = new Dictionary<TKey, TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
                throw new JsonException();

            if (!Enum.TryParse<TKey>(propertyName, ignoreCase: true, out var key))
            {
                reader.Read();
                reader.Skip();
                continue;
            }

            reader.Read();
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            if (value != null)
                dictionary[key] = value;
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key.ToString());
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// <summary>
/// Represents an available LLM model option for the chat interface.
/// Includes both built-in models from major providers and custom user-defined models.
/// </summary>
public class ModelOption
{
    /// <summary>Unique identifier for the model.</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>Human-readable display name for the UI.</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider name as string (for JSON serialization).
    /// Use ProviderEnum for programmatic access.
    /// </summary>
    [JsonIgnore]
    public string Provider 
    { 
        get => ProviderEnum.ToString();
        set 
        {
            if (Enum.TryParse<LLmProviders>(value, ignoreCase: true, out var parsed))
                ProviderEnum = parsed;
        }
    }
    
    /// <summary>The LLM provider for this model.</summary>
    public LLmProviders ProviderEnum { get; set; } = LLmProviders.OpenAi;
    
    /// <summary>Whether this is a user-defined custom model.</summary>
    public bool IsCustom { get; set; }
    
    /// <summary>
    /// API-specific model name if different from the Id.
    /// Used when the API requires a different identifier than what we display.
    /// </summary>
    public string? ApiName { get; set; }
    
    /// <summary>Maximum context window size in tokens.</summary>
    public int MaxContextTokens { get; set; }
    
    /// <summary>
    /// Whether this model supports tool/function calling.
    /// </summary>
    public bool SupportsToolCalls { get; set; } = true;
    
    /// <summary>
    /// Whether this model supports vision/image inputs.
    /// </summary>
    public bool SupportsVision { get; set; }
    
    /// <summary>
    /// Optional description or notes about the model.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets the default well-known models from major providers.
    /// This serves as a fallback when dynamic model discovery is unavailable.
    /// </summary>
    /// <returns>List of popular models from OpenAI, Anthropic, Google, and other providers.</returns>
    public static List<ModelOption> GetDefaultModels()
    {
        return
        [
            // OpenAI Models
            new ModelOption { Id = "gpt-4o", DisplayName = "GPT-4o", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000, SupportsVision = true, Description = "Most capable GPT-4 model" },
            new ModelOption { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000, SupportsVision = true, Description = "Smaller, faster GPT-4" },
            new ModelOption { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000, SupportsVision = true },
            new ModelOption { Id = "gpt-3.5-turbo", DisplayName = "GPT-3.5 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 16385 },
            new ModelOption { Id = "o3-mini", DisplayName = "o3 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000, Description = "Reasoning model" },
            new ModelOption { Id = "o4-mini", DisplayName = "o4 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 200000, Description = "Latest reasoning model" },
            
            // Anthropic Models  
            new ModelOption { Id = "claude-sonnet-4-20250514", DisplayName = "Claude Sonnet 4", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000, SupportsVision = true, Description = "Best for coding and analysis" },
            new ModelOption { Id = "claude-3-5-sonnet-latest", DisplayName = "Claude 3.5 Sonnet", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000, SupportsVision = true },
            new ModelOption { Id = "claude-3-5-haiku-latest", DisplayName = "Claude 3.5 Haiku", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000, Description = "Fast and efficient" },
            new ModelOption { Id = "claude-3-opus-latest", DisplayName = "Claude 3 Opus", ProviderEnum = LLmProviders.Anthropic, MaxContextTokens = 200000, SupportsVision = true, Description = "Most powerful Claude" },
            
            // Google Models
            new ModelOption { Id = "gemini-2.0-flash", DisplayName = "Gemini 2.0 Flash", ProviderEnum = LLmProviders.Google, MaxContextTokens = 1048576, SupportsVision = true, Description = "Latest Gemini" },
            new ModelOption { Id = "gemini-1.5-pro", DisplayName = "Gemini 1.5 Pro", ProviderEnum = LLmProviders.Google, MaxContextTokens = 2097152, SupportsVision = true, Description = "2M token context" },
            new ModelOption { Id = "gemini-1.5-flash", DisplayName = "Gemini 1.5 Flash", ProviderEnum = LLmProviders.Google, MaxContextTokens = 1048576, SupportsVision = true },
            
            // Groq Models (fast inference)
            new ModelOption { Id = "llama-3.3-70b-versatile", DisplayName = "Llama 3.3 70B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 131072, Description = "Via Groq" },
            new ModelOption { Id = "mixtral-8x7b-32768", DisplayName = "Mixtral 8x7B", ProviderEnum = LLmProviders.Groq, MaxContextTokens = 32768, Description = "Via Groq" },
        ];
    }
    
    /// <summary>
    /// Gets models for a specific provider.
    /// </summary>
    /// <param name="provider">The provider to filter by.</param>
    /// <returns>List of models for the specified provider.</returns>
    public static List<ModelOption> GetModelsForProvider(LLmProviders provider)
    {
        return GetDefaultModels().Where(m => m.ProviderEnum == provider).ToList();
    }
    
    /// <summary>
    /// Creates a display string showing the model name and provider.
    /// </summary>
    public override string ToString() => $"{DisplayName} ({Provider})";
}
