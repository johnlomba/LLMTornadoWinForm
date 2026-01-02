using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Batch;

/// <summary>
/// Represents a batch of requests.
/// </summary>
public class BatchItem
{
    /// <summary>
    /// Unique identifier for the batch.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Object type identifier.
    /// </summary>
    [JsonProperty("object")]
    public string? Object { get; set; }
    
    /// <summary>
    /// Type identifier (alternative property name used by some providers).
    /// </summary>
    [JsonProperty("type")]
    internal string? TypeInternal { get; set; }
    
    /// <summary>
    /// The endpoint used for the batch.
    /// </summary>
    [JsonProperty("endpoint")]
    public string? Endpoint { get; set; }
    
    /// <summary>
    /// Processing status of the batch (primary).
    /// </summary>
    [JsonProperty("status")]
    internal BatchStatus? StatusInternal { get; set; }
    
    /// <summary>
    /// Processing status (alternative property name used by some providers).
    /// </summary>
    [JsonProperty("processing_status")]
    internal BatchStatus? ProcessingStatusInternal { get; set; }
    
    /// <summary>
    /// State set by Google/Gemini provider (internal, mapped to StatusInternal).
    /// </summary>
    [JsonIgnore]
    internal BatchStatus? StateInternal { get; set; }
    
    /// <summary>
    /// Processing status of the batch.
    /// </summary>
    [JsonIgnore]
    public BatchStatus Status => StatusInternal ?? ProcessingStatusInternal ?? StateInternal ?? BatchStatus.Unknown;
    
    /// <summary>
    /// Full batch name/path used by Google/Gemini (e.g., "batches/123456").
    /// </summary>
    [JsonIgnore]
    internal string? NameInternal { get; set; }
    
    /// <summary>
    /// Display name set by the user (Google/Gemini).
    /// </summary>
    [JsonIgnore]
    internal string? DisplayNameInternal { get; set; }
    
    /// <summary>
    /// Display name of the batch (used by Google/Gemini).
    /// </summary>
    [JsonIgnore]
    public string? DisplayName => DisplayNameInternal;
    
    /// <summary>
    /// Inlined responses from Google/Gemini (internal use for result streaming).
    /// </summary>
    [JsonIgnore]
    internal JArray? GoogleInlinedResponses { get; set; }
    
    /// <summary>
    /// Errors that occurred during batch processing.
    /// </summary>
    [JsonProperty("errors")]
    public BatchErrors? Errors { get; set; }
    
    /// <summary>
    /// ID of the input file for the batch.
    /// </summary>
    [JsonProperty("input_file_id")]
    public string? InputFileId { get; set; }
    
    /// <summary>
    /// The time frame within which the batch should be processed.
    /// </summary>
    [JsonProperty("completion_window")]
    public BatchCompletionWindow? CompletionWindow { get; set; }
    
    // Internal raw timestamp properties for deserialization
    [JsonProperty("created_at")]
    internal object? CreatedAtRaw { get; set; }
    
    [JsonProperty("expires_at")]
    internal object? ExpiresAtRaw { get; set; }
    
    [JsonProperty("in_progress_at")]
    internal object? InProgressAtRaw { get; set; }
    
    [JsonProperty("finalizing_at")]
    internal object? FinalizingAtRaw { get; set; }
    
    [JsonProperty("completed_at")]
    internal object? CompletedAtRaw { get; set; }
    
    [JsonProperty("ended_at")]
    internal object? EndedAtRaw { get; set; }
    
    [JsonProperty("failed_at")]
    internal object? FailedAtRaw { get; set; }
    
    [JsonProperty("expired_at")]
    internal object? ExpiredAtRaw { get; set; }
    
    [JsonProperty("cancelling_at")]
    internal object? CancellingAtRaw { get; set; }
    
    [JsonProperty("cancel_initiated_at")]
    internal object? CancelInitiatedAtRaw { get; set; }
    
    [JsonProperty("cancelled_at")]
    internal object? CancelledAtRaw { get; set; }
    
    [JsonProperty("archived_at")]
    internal object? ArchivedAtRaw { get; set; }
    
    /// <summary>
    /// When the batch was created.
    /// </summary>
    [JsonIgnore]
    public DateTime? CreatedAt => ParseDateTime(CreatedAtRaw);
    
    /// <summary>
    /// When the batch will expire.
    /// </summary>
    [JsonIgnore]
    public DateTime? ExpiresAt => ParseDateTime(ExpiresAtRaw);
    
    /// <summary>
    /// When the batch started processing.
    /// </summary>
    [JsonIgnore]
    public DateTime? InProgressAt => ParseDateTime(InProgressAtRaw);
    
    /// <summary>
    /// When the batch started finalizing.
    /// </summary>
    [JsonIgnore]
    public DateTime? FinalizingAt => ParseDateTime(FinalizingAtRaw);
    
    /// <summary>
    /// When the batch completed. Harmonizes completed_at and ended_at from different providers.
    /// </summary>
    [JsonIgnore]
    public DateTime? CompletedAt => ParseDateTime(CompletedAtRaw) ?? ParseDateTime(EndedAtRaw);
    
    /// <summary>
    /// When the batch failed.
    /// </summary>
    [JsonIgnore]
    public DateTime? FailedAt => ParseDateTime(FailedAtRaw);
    
    /// <summary>
    /// When the batch expired.
    /// </summary>
    [JsonIgnore]
    public DateTime? ExpiredAt => ParseDateTime(ExpiredAtRaw);
    
    /// <summary>
    /// When cancellation was initiated. Harmonizes cancelling_at and cancel_initiated_at from different providers.
    /// </summary>
    [JsonIgnore]
    public DateTime? CancellingAt => ParseDateTime(CancellingAtRaw) ?? ParseDateTime(CancelInitiatedAtRaw);
    
    /// <summary>
    /// When the batch was cancelled.
    /// </summary>
    [JsonIgnore]
    public DateTime? CancelledAt => ParseDateTime(CancelledAtRaw);
    
    /// <summary>
    /// When the batch was archived.
    /// </summary>
    [JsonIgnore]
    public DateTime? ArchivedAt => ParseDateTime(ArchivedAtRaw);
    
    /// <summary>
    /// Request counts categorized by status.
    /// </summary>
    [JsonProperty("request_counts")]
    public BatchRequestCounts? RequestCounts { get; set; }
    
    /// <summary>
    /// ID of the file containing successful outputs.
    /// </summary>
    [JsonProperty("output_file_id")]
    public string? OutputFileId { get; set; }
    
    /// <summary>
    /// ID of the file containing error outputs.
    /// </summary>
    [JsonProperty("error_file_id")]
    public string? ErrorFileId { get; set; }
    
    /// <summary>
    /// URL to download results.
    /// </summary>
    [JsonProperty("results_url")]
    public string? ResultsUrl { get; set; }
    
    /// <summary>
    /// Metadata attached to the batch.
    /// </summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    /// <summary>
    /// The model used for the batch.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }

    private static DateTime? ParseDateTime(object? value)
    {
        return value switch
        {
            null => null,
            long unixTimestamp => DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime,
            int unixTimestamp => DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime,
            double unixTimestamp => DateTimeOffset.FromUnixTimeSeconds((long)unixTimestamp).UtcDateTime,
            string dateString when DateTime.TryParse(dateString, out DateTime parsed) => parsed,
            _ => null
        };
    }
}
