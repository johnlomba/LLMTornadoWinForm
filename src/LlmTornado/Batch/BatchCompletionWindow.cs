using System;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// The time frame within which the batch should be processed.
/// </summary>
[JsonConverter(typeof(BatchCompletionWindowConverter))]
public class BatchCompletionWindow
{
    /// <summary>
    /// 24 hours completion window.
    /// </summary>
    public static readonly BatchCompletionWindow Hours24 = new BatchCompletionWindow("24h");
    
    /// <summary>
    /// The string value of the completion window.
    /// </summary>
    public string Value { get; }
    
    /// <summary>
    /// Creates a completion window with a custom value.
    /// </summary>
    /// <param name="value">The custom completion window string</param>
    public BatchCompletionWindow(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    /// <summary>
    /// Creates a custom completion window.
    /// </summary>
    /// <param name="value">The custom completion window string</param>
    /// <returns>A new BatchCompletionWindow instance</returns>
    public static BatchCompletionWindow Custom(string value) => new BatchCompletionWindow(value);
    
    /// <summary>
    /// Implicit conversion from string.
    /// </summary>
    public static implicit operator BatchCompletionWindow(string value) => new BatchCompletionWindow(value);
    
    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(BatchCompletionWindow window) => window.Value;
    
    /// <inheritdoc />
    public override string ToString() => Value;
    
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            BatchCompletionWindow other => Value == other.Value,
            string str => Value == str,
            _ => false
        };
    }
    
    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();
}

internal class BatchCompletionWindowConverter : JsonConverter<BatchCompletionWindow>
{
    public override void WriteJson(JsonWriter writer, BatchCompletionWindow? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.Value);
    }

    public override BatchCompletionWindow? ReadJson(JsonReader reader, Type objectType, BatchCompletionWindow? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        
        string? value = reader.Value?.ToString();
        return value is null ? null : new BatchCompletionWindow(value);
    }
}
