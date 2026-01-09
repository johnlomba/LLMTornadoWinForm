using System.ComponentModel;

namespace LlmTornado.Agents.Samples.ErpAgent.DataModels;

/// <summary>
/// Result of reviewing query execution results.
/// </summary>
public class ReviewResult
{
    /// <summary>
    /// Whether the results successfully answer the original question.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The execution result being reviewed.
    /// </summary>
    public QueryExecutionResult ExecutionResult { get; set; } = new();

    /// <summary>
    /// The formatted data to return (used when Success = true).
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// Partial/best-effort data (used when max attempts reached).
    /// </summary>
    public string PartialData { get; set; } = "";

    /// <summary>
    /// Revised plan for correction (used when Success = false).
    /// </summary>
    public SqlQueryPlan CorrectionPlan { get; set; }

    /// <summary>
    /// How many correction attempts have been made.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Issues found during review.
    /// </summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// Hints for the corrector on what to fix.
    /// </summary>
    public string CorrectionHint { get; set; } = "";
}

/// <summary>
/// Structured output schema for the reviewer LLM.
/// </summary>
[Description("Review verdict for query execution results")]
public struct ReviewVerdict
{
    /// <summary>
    /// Does the data adequately answer the original question?
    /// </summary>
    [Description("Does the data adequately answer the original question?")]
    public bool AnswersQuestion { get; set; }

    /// <summary>
    /// Does the data look correct (no obvious errors, nulls, wrong types)?
    /// </summary>
    [Description("Does the data look correct and well-formed?")]
    public bool DataLooksCorrect { get; set; }

    /// <summary>
    /// List of specific issues found.
    /// </summary>
    [Description("Specific issues found with the results")]
    public string[] Issues { get; set; }

    /// <summary>
    /// Hint for what needs to be fixed.
    /// </summary>
    [Description("Guidance on what needs to be corrected")]
    public string CorrectionHint { get; set; }

    /// <summary>
    /// Confidence score 0-100.
    /// </summary>
    [Description("Confidence that this answers the question (0-100)")]
    public int ConfidenceScore { get; set; }
}

