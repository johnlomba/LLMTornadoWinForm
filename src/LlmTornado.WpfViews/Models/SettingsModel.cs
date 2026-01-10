using System.Text.Json;
using System.Text.Json.Serialization;
using LlmTornado.Code;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Application settings model.
/// </summary>
public class SettingsModel
{
    /// <summary>
    /// API keys for each provider. Key is the provider enum, value is the API key.
    /// For Azure, the key should be stored with special handling for endpoint.
    /// </summary>
    [JsonConverter(typeof(DictionaryJsonConverter<LLmProviders, string>))]
    public Dictionary<LLmProviders, string> ApiKeys { get; set; } = new();
    
    /// <summary>
    /// Custom endpoints for self-hosted providers (like Ollama, LM Studio).
    /// Key is the provider enum, value is the endpoint URL.
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
    
    // Legacy properties for backward compatibility (marked as obsolete)
    /// <summary>
    /// Legacy OpenAI API key. Use ApiKeys[LLmProviders.OpenAi] instead.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OpenAiApiKey { get; set; }
    
    /// <summary>
    /// Legacy Azure API key. Use ApiKeys[LLmProviders.AzureOpenAi] instead.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AzureApiKey { get; set; }
    
    /// <summary>
    /// Legacy flag for Azure. Check ApiKeys.ContainsKey(LLmProviders.AzureOpenAi) instead.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseAzure { get; set; }
}

/// <summary>
/// Custom model option defined by the user.
/// </summary>
public class CustomModelOption
{
    /// <summary>
    /// Model ID/name.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the model.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider for this model.
    /// </summary>
    public LLmProviders Provider { get; set; }
    
    /// <summary>
    /// Provider-specific API name (if different from Id).
    /// </summary>
    public string? ApiName { get; set; }
    
    /// <summary>
    /// Maximum context tokens.
    /// </summary>
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
        {
            throw new JsonException();
        }

        var dictionary = new Dictionary<TKey, TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new JsonException();
            }

            if (!Enum.TryParse<TKey>(propertyName, ignoreCase: true, out var key))
            {
                // Skip unknown enum values
                reader.Read();
                reader.Skip();
                continue;
            }

            reader.Read();
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            if (value != null)
            {
                dictionary[key] = value;
            }
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
/// Available model options.
/// </summary>
public class ModelOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Provider as string (for backward compatibility with old UI bindings).
    /// </summary>
    [JsonIgnore]
    public string Provider 
    { 
        get => ProviderEnum.ToString();
        set 
        {
            if (Enum.TryParse<LLmProviders>(value, ignoreCase: true, out var parsed))
            {
                ProviderEnum = parsed;
            }
        }
    }
    
    /// <summary>
    /// Provider as enum.
    /// </summary>
    public LLmProviders ProviderEnum { get; set; } = LLmProviders.OpenAi;
    
    /// <summary>
    /// Whether this is a custom model defined by the user.
    /// </summary>
    public bool IsCustom { get; set; }
    
    /// <summary>
    /// Provider-specific API name (if different from Id).
    /// </summary>
    public string? ApiName { get; set; }
    
    public int MaxContextTokens { get; set; }
    
    public static List<ModelOption> GetDefaultModels()
    {
        return
        [
            new ModelOption { Id = "gpt-4o", DisplayName = "GPT-4o", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
            new ModelOption { Id = "gpt-3.5-turbo", DisplayName = "GPT-3.5 Turbo", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 16385 },
            new ModelOption { Id = "o3-mini", DisplayName = "o3 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 128000 },
            new ModelOption { Id = "o4-mini", DisplayName = "o4 Mini", ProviderEnum = LLmProviders.OpenAi, MaxContextTokens = 200000 }
        ];
    }
}

