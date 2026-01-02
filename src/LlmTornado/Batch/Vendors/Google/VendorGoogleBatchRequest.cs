using System.Collections.Generic;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Batch.Vendors.Google;

/// <summary>
/// Google/Gemini-specific batch request metadata for each inlined request.
/// </summary>
internal class VendorGoogleBatchRequestMetadata
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Google/Gemini-specific inlined request item.
/// </summary>
internal class VendorGoogleBatchInlinedRequest
{
    [JsonProperty("request")]
    public VendorGoogleChatRequest Request { get; set; }
    
    [JsonProperty("metadata")]
    public VendorGoogleBatchRequestMetadata Metadata { get; set; }
    
    public VendorGoogleBatchInlinedRequest(BatchRequestItem item, IEndpointProvider provider)
    {
        Request = new VendorGoogleChatRequest(item.Params, provider);
        Metadata = new VendorGoogleBatchRequestMetadata
        {
            Key = item.CustomId
        };
    }
}

/// <summary>
/// Wrapper for inlined requests array.
/// </summary>
internal class VendorGoogleBatchInlinedRequestsWrapper
{
    [JsonProperty("requests")]
    public List<VendorGoogleBatchInlinedRequest> Requests { get; set; } = [];
}

/// <summary>
/// Input configuration for the batch.
/// </summary>
internal class VendorGoogleBatchInputConfig
{
    [JsonProperty("requests")]
    public VendorGoogleBatchInlinedRequestsWrapper? Requests { get; set; }
    
    [JsonProperty("file_name")]
    public string? FileName { get; set; }
}

/// <summary>
/// Batch configuration object.
/// </summary>
internal class VendorGoogleBatchConfig
{
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }
    
    [JsonProperty("input_config")]
    public VendorGoogleBatchInputConfig InputConfig { get; set; } = new();
    
    [JsonProperty("priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Google/Gemini batch creation request body.
/// </summary>
internal class VendorGoogleBatchRequest
{
    [JsonProperty("batch")]
    public VendorGoogleBatchConfig Batch { get; set; } = new();
    
    public VendorGoogleBatchRequest(BatchRequest request, IEndpointProvider provider)
    {
        Batch.InputConfig.Requests = new VendorGoogleBatchInlinedRequestsWrapper();
        
        foreach (BatchRequestItem item in request.Requests)
        {
            Batch.InputConfig.Requests.Requests.Add(new VendorGoogleBatchInlinedRequest(item, provider));
        }
        
        // Set display name from vendor extensions if provided
        Batch.DisplayName = request.VendorExtensions?.Google?.DisplayName ?? $"batch_{System.Guid.NewGuid():N}";
        
        // Set priority if provided
        if (request.VendorExtensions?.Google?.Priority is not null)
        {
            Batch.Priority = request.VendorExtensions.Google.Priority.Value.ToString();
        }
    }
    
    /// <summary>
    /// Serializes the request to JSON.
    /// </summary>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this, EndpointBase.NullSettings);
    }
}
