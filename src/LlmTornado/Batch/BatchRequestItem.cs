using LlmTornado.Chat;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// An individual request in a batch.
/// </summary>
public class BatchRequestItem
{
    /// <summary>
    /// Developer-provided ID for matching results to requests.
    /// Must be unique for each request within the batch.
    /// </summary>
    [JsonProperty("custom_id")]
    public string CustomId { get; set; } = string.Empty;
    
    /// <summary>
    /// The chat request parameters for this batch item.
    /// </summary>
    [JsonProperty("params")]
    public ChatRequest Params { get; set; } = new();
    
    /// <summary>
    /// Creates an empty batch request item.
    /// </summary>
    public BatchRequestItem()
    {
    }
    
    /// <summary>
    /// Creates a batch request item with the specified custom ID and parameters.
    /// </summary>
    /// <param name="customId">Developer-provided ID for matching results</param>
    /// <param name="chatRequest">The chat request parameters</param>
    public BatchRequestItem(string customId, ChatRequest chatRequest)
    {
        CustomId = customId;
        Params = chatRequest;
    }
}
