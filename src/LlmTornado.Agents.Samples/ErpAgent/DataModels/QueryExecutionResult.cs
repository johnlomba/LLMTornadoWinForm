namespace LlmTornado.Agents.Samples.ErpAgent.DataModels;

/// <summary>
/// Results from executing SQL queries against the ERP database.
/// </summary>
public class QueryExecutionResult
{
    /// <summary>
    /// The original plan that was executed.
    /// </summary>
    public SqlQueryPlan Plan { get; set; }

    /// <summary>
    /// Combined data results from all queries (typically JSON or tabular text).
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// Whether all queries executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Individual results per query step.
    /// </summary>
    public List<StepResult> StepResults { get; set; } = [];

    /// <summary>
    /// Any errors encountered during execution.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Schema information gathered during inspection.
    /// </summary>
    public string SchemaContext { get; set; } = "";
}

/// <summary>
/// Result of executing a single query step.
/// </summary>
public class StepResult
{
    /// <summary>
    /// The step that was executed.
    /// </summary>
    public SqlQueryStep Step { get; set; }

    /// <summary>
    /// Whether this step succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Query result data (JSON rows or error message).
    /// </summary>
    public string Data { get; set; } = "";

    /// <summary>
    /// Number of rows returned.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; set; }
}

