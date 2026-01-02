using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Response from deleting a batch.
/// </summary>
public class DeletedBatch
{
    /// <summary>
    /// ID of the deleted batch.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Object type for Anthropic.
    /// </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
}
