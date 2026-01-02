using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Batch.Vendors.Google;

/// <summary>
/// Google/Gemini batch statistics.
/// </summary>
internal class VendorGoogleBatchStats
{
    [JsonProperty("requestCount")]
    public string? RequestCount { get; set; }
    
    [JsonProperty("successfulRequestCount")]
    public string? SuccessfulRequestCount { get; set; }
    
    [JsonProperty("failedRequestCount")]
    public string? FailedRequestCount { get; set; }
    
    [JsonProperty("pendingRequestCount")]
    public string? PendingRequestCount { get; set; }
}

/// <summary>
/// Google/Gemini batch output destination.
/// </summary>
internal class VendorGoogleBatchOutput
{
    [JsonProperty("fileName")]
    public string? FileName { get; set; }
    
    [JsonProperty("responsesFile")]
    public string? ResponsesFile { get; set; }
    
    [JsonProperty("inlinedResponses")]
    public JArray? InlinedResponses { get; set; }
}

/// <summary>
/// Google/Gemini batch metadata within Operation.
/// </summary>
internal class VendorGoogleBatchMetadata
{
    [JsonProperty("@type")]
    public string? Type { get; set; }
    
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
    
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    [JsonProperty("state")]
    public string? State { get; set; }
    
    [JsonProperty("createTime")]
    public string? CreateTime { get; set; }
    
    [JsonProperty("updateTime")]
    public string? UpdateTime { get; set; }
    
    [JsonProperty("endTime")]
    public string? EndTime { get; set; }
    
    [JsonProperty("batchStats")]
    public VendorGoogleBatchStats? BatchStats { get; set; }
    
    [JsonProperty("priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Google/Gemini ListOperationsResponse for batch listing.
/// </summary>
internal class VendorGoogleBatchListResponse
{
    [JsonProperty("operations")]
    public JArray? Operations { get; set; }
    
    [JsonProperty("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Google/Gemini Operation response wrapper.
/// </summary>
internal class VendorGoogleBatchOperation
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("done")]
    public bool Done { get; set; }
    
    [JsonProperty("metadata")]
    public VendorGoogleBatchMetadata? Metadata { get; set; }
    
    [JsonProperty("response")]
    public VendorGoogleBatchOutput? Response { get; set; }
    
    [JsonProperty("error")]
    public JToken? Error { get; set; }
}

/// <summary>
/// Handles conversion of Google/Gemini Operation response to unified BatchItem.
/// </summary>
internal static class VendorGoogleBatchItem
{
    /// <summary>
    /// Maps Gemini batch state to unified BatchStatus.
    /// </summary>
    private static BatchStatus MapState(string? state)
    {
        return state switch
        {
            "JOB_STATE_PENDING" or "BATCH_STATE_PENDING" => BatchStatus.Validating,
            "JOB_STATE_RUNNING" or "BATCH_STATE_RUNNING" => BatchStatus.InProgress,
            "JOB_STATE_SUCCEEDED" or "BATCH_STATE_SUCCEEDED" => BatchStatus.Completed,
            "JOB_STATE_FAILED" or "BATCH_STATE_FAILED" => BatchStatus.Failed,
            "JOB_STATE_CANCELLED" or "BATCH_STATE_CANCELLED" => BatchStatus.Cancelled,
            "JOB_STATE_EXPIRED" or "BATCH_STATE_EXPIRED" => BatchStatus.Expired,
            _ => BatchStatus.Unknown
        };
    }
    
    /// <summary>
    /// Extracts batch ID from the full name (e.g., "batches/123" -> "123" or returns full name).
    /// </summary>
    private static string ExtractBatchId(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }
        
        // Keep the full name as ID for Gemini since it's used in API calls
        return name;
    }
    
    /// <summary>
    /// Parses output section and populates BatchItem with inlined responses or output file ID.
    /// </summary>
    private static void ParseOutput(JToken output, BatchItem item)
    {
        // Check for responsesFile (file-based output)
        string? responsesFile = output["responsesFile"]?.Value<string>();
        if (!string.IsNullOrEmpty(responsesFile))
        {
            item.OutputFileId = responsesFile;
        }
        
        // Check for inlinedResponses (inline output)
        JToken? inlinedResponses = output["inlinedResponses"];
        if (inlinedResponses is not null)
        {
            // inlinedResponses contains an array of InlinedResponse objects
            JArray? responsesArray = inlinedResponses["inlinedResponses"] as JArray ?? inlinedResponses as JArray;
            if (responsesArray is not null)
            {
                item.GoogleInlinedResponses = responsesArray;
            }
        }
    }
    
    /// <summary>
    /// Deserializes a Google/Gemini Operation response to a unified BatchItem.
    /// </summary>
    /// <param name="jsonData">The JSON data to deserialize.</param>
    /// <returns>The deserialized batch item.</returns>
    public static BatchItem? Deserialize(string jsonData)
    {
        try
        {
            JObject? json = JObject.Parse(jsonData);
            if (json is null)
            {
                return null;
            }
            
            BatchItem item = new BatchItem();
            
            // Get top-level name
            string? topName = json["name"]?.Value<string>();
            
            // Parse metadata object directly using JToken for reliability
            JToken? metadata = json["metadata"];
            if (metadata is not null)
            {
                string? metaName = metadata["name"]?.Value<string>();
                string? displayName = metadata["displayName"]?.Value<string>();
                string? model = metadata["model"]?.Value<string>();
                string? state = metadata["state"]?.Value<string>();
                string? createTime = metadata["createTime"]?.Value<string>();
                string? endTime = metadata["endTime"]?.Value<string>();
                
                item.Id = ExtractBatchId(metaName ?? topName);
                item.NameInternal = metaName ?? topName;
                item.DisplayNameInternal = displayName;
                item.Model = model;
                item.StateInternal = MapState(state);
                item.CreatedAtRaw = createTime;
                item.EndedAtRaw = endTime;
                
                // Parse batch stats
                JToken? batchStats = metadata["batchStats"];
                if (batchStats is not null)
                {
                    int.TryParse(batchStats["requestCount"]?.Value<string>(), out int total);
                    int.TryParse(batchStats["successfulRequestCount"]?.Value<string>(), out int success);
                    int.TryParse(batchStats["failedRequestCount"]?.Value<string>(), out int failed);
                    
                    item.RequestCounts = new BatchRequestCounts
                    {
                        Total = total,
                        SucceededInternal = success,
                        FailedInternal = failed
                    };
                }
                
                // Parse output from metadata (contains inlinedResponses or responsesFile)
                JToken? output = metadata["output"];
                if (output is not null)
                {
                    ParseOutput(output, item);
                }
            }
            else
            {
                // Direct batch object (less common) - no metadata wrapper
                item.Id = ExtractBatchId(topName);
                item.NameInternal = topName;
            }
            
            // Also check top-level response field (Operation wrapper)
            JToken? response = json["response"];
            if (response is not null)
            {
                // The response might contain output directly
                JToken? responseOutput = response["output"];
                if (responseOutput is not null)
                {
                    ParseOutput(responseOutput, item);
                }
                else
                {
                    // Or it might be the output itself
                    ParseOutput(response, item);
                }
            }
            
            // Handle error
            JToken? error = json["error"];
            if (error is not null)
            {
                item.Errors = new BatchErrors
                {
                    Data =
                    [
                        new BatchError
                        {
                            Message = error["message"]?.Value<string>(),
                            Code = error["code"]?.Value<string>()
                        }
                    ]
                };
            }
            
            return item;
        }
        catch
        {
            return null;
        }
    }
}
