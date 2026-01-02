using Newtonsoft.Json;

namespace LlmTornado.Batch.Vendors.OpenAi;

/// <summary>
/// Handles deserialization of OpenAI batch results.
/// </summary>
internal static class VendorOpenAiBatchResult
{
    /// <summary>
    /// Deserializes an OpenAI batch result from JSON.
    /// </summary>
    /// <param name="jsonData">The JSON data to deserialize.</param>
    /// <returns>The deserialized batch result.</returns>
    public static BatchResult? Deserialize(string jsonData)
    {
        BatchResult? result = JsonConvert.DeserializeObject<BatchResult>(jsonData);
        if (result is not null)
        {
            result.RawResponse = jsonData;
        }
        return result;
    }
}
