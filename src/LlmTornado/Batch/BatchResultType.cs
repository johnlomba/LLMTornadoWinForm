using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Batch;

/// <summary>
/// Type of individual batch result.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum BatchResultType
{
    /// <summary>
    /// Unknown result type.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// The request succeeded.
    /// </summary>
    [EnumMember(Value = "succeeded")]
    Succeeded,
    
    /// <summary>
    /// The request errored.
    /// </summary>
    [EnumMember(Value = "errored")]
    Errored,
    
    /// <summary>
    /// The request was cancelled.
    /// </summary>
    [EnumMember(Value = "canceled")]
    Cancelled,
    
    /// <summary>
    /// The request expired.
    /// </summary>
    [EnumMember(Value = "expired")]
    Expired
}
