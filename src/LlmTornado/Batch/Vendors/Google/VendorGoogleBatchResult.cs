using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Cohere;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Batch.Vendors.Google;

/// <summary>
/// Handles deserialization of Google/Gemini batch results.
/// </summary>
internal static class VendorGoogleBatchResult
{
    /// <summary>
    /// Deserializes a Google/Gemini batch result from JSON.
    /// The response can come from either inlinedResponses or a file download.
    /// </summary>
    /// <param name="jsonData">The JSON data to deserialize.</param>
    /// <returns>The deserialized batch result.</returns>
    public static BatchResult? Deserialize(string jsonData)
    {
        try
        {
            JObject? json = JObject.Parse(jsonData);
            if (json is null)
            {
                return null;
            }
            
            BatchResult result = new BatchResult
            {
                RawResponse = jsonData
            };
            
            // Parse the metadata key (custom_id equivalent)
            JToken? metadataToken = json["metadata"];
            if (metadataToken is not null)
            {
                result.CustomId = metadataToken["key"]?.Value<string>() ?? string.Empty;
            }
            
            // Also check for "key" directly at top level (file-based output format)
            string? keyDirect = json["key"]?.Value<string>();
            if (!string.IsNullOrEmpty(keyDirect))
            {
                result.CustomId = keyDirect;
            }
            
            // Parse error if present at top level
            JToken? errorToken = json["error"];
            if (errorToken is not null)
            {
                result.Error = new BatchError
                {
                    Message = errorToken["message"]?.Value<string>(),
                    Code = errorToken["code"]?.Value<string>()
                };
                
                result.ResultInternal = new BatchResultContent
                {
                    Type = BatchResultType.Errored
                };
                
                return result;
            }
            
            // Parse the response (GenerateContentResponse)
            JToken? responseToken = json["response"];
            if (responseToken is not null)
            {
                string responseJson = responseToken.ToString(Formatting.None);
                VendorGoogleChatResult? googleResult = JsonConvert.DeserializeObject<VendorGoogleChatResult>(responseJson);
                
                if (googleResult is not null)
                {
                    ChatResult chatResult = googleResult.ToChatResult(null, null);
                    
                    result.ResultInternal = new BatchResultContent
                    {
                        Type = BatchResultType.Succeeded,
                        Message = chatResult
                    };
                }
            }
            
            return result;
        }
        catch
        {
            // Fall back to standard deserialization
            BatchResult? result = JsonConvert.DeserializeObject<BatchResult>(jsonData);
            if (result is not null)
            {
                result.RawResponse = jsonData;
            }
            return result;
        }
    }
    
    /// <summary>
    /// Deserializes an inlined response from the batch output.
    /// </summary>
    /// <param name="inlinedResponse">The inlined response JSON object.</param>
    /// <returns>The deserialized batch result.</returns>
    public static BatchResult? DeserializeInlined(JToken inlinedResponse)
    {
        try
        {
            BatchResult result = new BatchResult
            {
                RawResponse = inlinedResponse.ToString(Formatting.None)
            };
            
            // Parse metadata for the key
            JToken? metadataToken = inlinedResponse["metadata"];
            if (metadataToken is not null)
            {
                result.CustomId = metadataToken["key"]?.Value<string>() ?? string.Empty;
            }
            
            // Parse error if present
            JToken? errorToken = inlinedResponse["error"];
            if (errorToken is not null)
            {
                result.Error = new BatchError
                {
                    Message = errorToken["message"]?.Value<string>(),
                    Code = errorToken["code"]?.Value<string>()
                };
                
                result.ResultInternal = new BatchResultContent
                {
                    Type = BatchResultType.Errored
                };
                
                return result;
            }
            
            // Parse the response
            JToken? responseToken = inlinedResponse["response"];
            if (responseToken is not null)
            {
                string responseJson = responseToken.ToString(Formatting.None);
                VendorGoogleChatResult? googleResult = JsonConvert.DeserializeObject<VendorGoogleChatResult>(responseJson);
                
                if (googleResult is not null)
                {
                    ChatResult chatResult = googleResult.ToChatResult(null, null);
                    
                    result.ResultInternal = new BatchResultContent
                    {
                        Type = BatchResultType.Succeeded,
                        Message = chatResult
                    };
                }
            }
            
            return result;
        }
        catch
        {
            return null;
        }
    }
}
