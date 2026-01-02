using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Request to create a batch of chat completion requests.
/// </summary>
public class BatchRequest
{
    /// <summary>
    /// List of requests for the batch. Each is an individual request to create a message/completion.
    /// </summary>
    [JsonProperty("requests")]
    public List<BatchRequestItem> Requests { get; set; } = [];
    
    /// <summary>
    /// The time frame within which the batch should be processed.
    /// Currently only 24h is supported by most providers.
    /// </summary>
    [JsonProperty("completion_window")]
    public BatchCompletionWindow CompletionWindow { get; set; } = BatchCompletionWindow.Hours24;
    
    /// <summary>
    /// Vendor-specific extensions for the batch request.
    /// </summary>
    [JsonIgnore]
    public BatchRequestVendorExtensions? VendorExtensions { get; set; }
    
    /// <summary>
    /// Creates an empty batch request.
    /// </summary>
    public BatchRequest()
    {
    }
    
    /// <summary>
    /// Creates a batch request with the specified items.
    /// </summary>
    /// <param name="requests">The batch request items</param>
    public BatchRequest(IEnumerable<BatchRequestItem> requests)
    {
        Requests = [..requests];
    }
    
    /// <summary>
    /// Creates a batch request with the specified items and completion window.
    /// </summary>
    /// <param name="requests">The batch request items</param>
    /// <param name="completionWindow">The completion window</param>
    public BatchRequest(IEnumerable<BatchRequestItem> requests, BatchCompletionWindow completionWindow)
    {
        Requests = [..requests];
        CompletionWindow = completionWindow;
    }
}
