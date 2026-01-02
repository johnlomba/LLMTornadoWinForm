using System.Collections.Generic;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Batch.Vendors.OpenAi;

/// <summary>
/// OpenAI-specific batch request line item for JSONL format.
/// </summary>
internal class VendorOpenAiBatchRequestLine
{
    [JsonProperty("custom_id")]
    public string CustomId { get; set; } = string.Empty;
    
    [JsonProperty("method")]
    public string Method { get; set; } = "POST";
    
    [JsonProperty("url")]
    public string Url { get; set; } = "/v1/chat/completions";
    
    [JsonProperty("body")]
    public JObject Body { get; set; }
    
    public VendorOpenAiBatchRequestLine(BatchRequestItem item, IEndpointProvider provider)
    {
        CustomId = item.CustomId;
        
        // Ensure stream is null (batch doesn't support streaming)
        item.Params.Stream = null;
        
        // Use the ChatRequest's existing serialization which handles tools, response_format, etc.
        TornadoRequestContent serialized = item.Params.Serialize(provider);
        string bodyJson = serialized.Body as string ?? JsonConvert.SerializeObject(serialized.Body, EndpointBase.NullSettings);
        Body = JObject.Parse(bodyJson);
    }
}

/// <summary>
/// OpenAI-specific batch request handling.
/// </summary>
internal class VendorOpenAiBatchRequest
{
    /// <summary>
    /// Serializes batch request items to JSONL format for file upload.
    /// </summary>
    public static string SerializeToJsonl(BatchRequest request, IEndpointProvider provider)
    {
        StringBuilder sb = new StringBuilder();
        
        foreach (BatchRequestItem item in request.Requests)
        {
            VendorOpenAiBatchRequestLine line = new VendorOpenAiBatchRequestLine(item, provider);
            string json = JsonConvert.SerializeObject(line, EndpointBase.NullSettings);
            sb.AppendLine(json);
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Serializes batch request items to JSONL bytes for file upload.
    /// </summary>
    public static byte[] SerializeToJsonlBytes(BatchRequest request, IEndpointProvider provider)
    {
        return Encoding.UTF8.GetBytes(SerializeToJsonl(request, provider));
    }
}

/// <summary>
/// OpenAI batch creation request (after file is uploaded).
/// </summary>
internal class VendorOpenAiBatchCreateRequest
{
    [JsonProperty("input_file_id")]
    public string InputFileId { get; set; } = string.Empty;
    
    [JsonProperty("endpoint")]
    public string Endpoint { get; set; } = "/v1/chat/completions";
    
    [JsonProperty("completion_window")]
    public string CompletionWindow { get; set; } = "24h";
    
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
