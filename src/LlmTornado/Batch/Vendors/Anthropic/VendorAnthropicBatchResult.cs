using LlmTornado.Chat.Vendors.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Batch.Vendors.Anthropic;

/// <summary>
/// Handles deserialization of Anthropic batch results.
/// </summary>
internal static class VendorAnthropicBatchResult
{
    /// <summary>
    /// Deserializes an Anthropic batch result from JSON.
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
                CustomId = json["custom_id"]?.Value<string>() ?? string.Empty,
                Id = json["id"]?.Value<string>(),
                RawResponse = jsonData
            };
            
            // Parse the result object which contains type and message
            JToken? resultToken = json["result"];
            if (resultToken is not null)
            {
                string? typeStr = resultToken["type"]?.Value<string>();
                result.ResultInternal = new BatchResultContent
                {
                    Type = typeStr switch
                    {
                        "succeeded" => BatchResultType.Succeeded,
                        "errored" => BatchResultType.Errored,
                        "canceled" => BatchResultType.Cancelled,
                        "expired" => BatchResultType.Expired,
                        _ => BatchResultType.Unknown
                    }
                };
                
                // Parse the message using Anthropic's converter
                JToken? messageToken = resultToken["message"];
                if (messageToken is not null)
                {
                    string messageJson = messageToken.ToString(Formatting.None);
                    VendorAnthropicChatResult? anthropicResult = JsonConvert.DeserializeObject<VendorAnthropicChatResult>(messageJson);
                    if (anthropicResult is not null)
                    {
                        result.ResultInternal.Message = anthropicResult.ToChatResult(null, null);
                    }
                }
                
                // Parse error if present
                JToken? errorToken = resultToken["error"];
                if (errorToken is not null)
                {
                    result.ResultInternal.Error = errorToken.ToObject<BatchResultError>();
                }
            }
            
            // Parse top-level error if present
            JToken? topError = json["error"];
            if (topError is not null)
            {
                result.Error = topError.ToObject<BatchError>();
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
}
