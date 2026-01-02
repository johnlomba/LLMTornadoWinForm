using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Vendor-specific extensions for batch requests.
/// </summary>
public class BatchRequestVendorExtensions
{
    /// <summary>
    /// OpenAI-specific extensions.
    /// </summary>
    public BatchRequestVendorOpenAiExtensions? OpenAi { get; set; }
}

/// <summary>
/// OpenAI-specific batch request extensions.
/// </summary>
public class BatchRequestVendorOpenAiExtensions
{
    /// <summary>
    /// Set of key-value pairs that can be attached to the batch.
    /// Keys are strings with a maximum length of 64 characters.
    /// Values are strings with a maximum length of 512 characters.
    /// </summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
