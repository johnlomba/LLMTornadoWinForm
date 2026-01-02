using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Error information for a batch or batch result.
/// </summary>
public class BatchError
{
    /// <summary>
    /// An error code identifying the error type.
    /// </summary>
    [JsonProperty("code")]
    public string? Code { get; set; }
    
    /// <summary>
    /// A human-readable message providing more details about the error.
    /// </summary>
    [JsonProperty("message")]
    public string? Message { get; set; }
    
    /// <summary>
    /// The name of the parameter that caused the error, if applicable.
    /// </summary>
    [JsonProperty("param")]
    public string? Param { get; set; }
    
    /// <summary>
    /// The line number of the input file where the error occurred, if applicable.
    /// </summary>
    [JsonProperty("line")]
    public int? Line { get; set; }
    
    /// <summary>
    /// The error type.
    /// </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Container for batch errors.
/// </summary>
public class BatchErrors
{
    /// <summary>
    /// The object type, which is always "list".
    /// </summary>
    [JsonProperty("object")]
    public string? Object { get; set; }
    
    /// <summary>
    /// The list of errors.
    /// </summary>
    [JsonProperty("data")]
    public BatchError[]? Data { get; set; }
}
