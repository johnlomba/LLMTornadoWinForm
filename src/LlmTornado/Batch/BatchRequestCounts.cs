using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Tallies requests within the batch, categorized by their status.
/// </summary>
public class BatchRequestCounts
{
    /// <summary>
    /// Total number of requests in the batch.
    /// </summary>
    [JsonProperty("total")]
    public int Total { get; set; }
    
    // Internal properties for deserialization from different providers
    [JsonProperty("completed")]
    internal int? CompletedInternal { get; set; }
    
    [JsonProperty("succeeded")]
    internal int? SucceededInternal { get; set; }
    
    [JsonProperty("failed")]
    internal int? FailedInternal { get; set; }
    
    [JsonProperty("errored")]
    internal int? ErroredInternal { get; set; }
    
    /// <summary>
    /// Number of requests that have completed successfully.
    /// </summary>
    [JsonIgnore]
    public int Completed => CompletedInternal ?? SucceededInternal ?? 0;
    
    /// <summary>
    /// Number of requests that have failed.
    /// </summary>
    [JsonIgnore]
    public int Failed => FailedInternal ?? ErroredInternal ?? 0;
    
    /// <summary>
    /// Number of requests that are still processing.
    /// </summary>
    [JsonProperty("processing")]
    public int? Processing { get; set; }
    
    /// <summary>
    /// Number of requests that have been cancelled.
    /// </summary>
    [JsonProperty("canceled")]
    public int? Cancelled { get; set; }
    
    /// <summary>
    /// Number of requests that have expired.
    /// </summary>
    [JsonProperty("expired")]
    public int? Expired { get; set; }
}
