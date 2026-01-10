using System.ComponentModel;

namespace LlmTornado.Agents.Samples.ErpAgent.DataModels;

/// <summary>
/// Output schema for the planning phase - describes what SQL queries to execute.
/// </summary>
[Description("A plan for answering the user's question with SQL queries")]
public struct SqlQueryPlan
{
    /// <summary>
    /// The original user question being answered.
    /// </summary>
    [Description("The original question from the user")]
    public string OriginalQuestion { get; set; }

    /// <summary>
    /// Tables that are likely needed to answer the question.
    /// </summary>
    [Description("Database tables that are likely needed")]
    public string[] TableHints { get; set; }

    /// <summary>
    /// Ordered steps to execute.
    /// </summary>
    [Description("SQL query steps to execute in order")]
    public SqlQueryStep[] Steps { get; set; }

    /// <summary>
    /// Brief reasoning for the plan.
    /// </summary>
    [Description("Brief explanation of why this plan will answer the question")]
    public string Reasoning { get; set; }
}

/// <summary>
/// A single SQL query step in the plan.
/// </summary>
[Description("A single SQL query step")]
public struct SqlQueryStep
{
    /// <summary>
    /// What this step accomplishes.
    /// </summary>
    [Description("What this query step accomplishes")]
    public string Description { get; set; }

    /// <summary>
    /// The SQL query to execute.
    /// </summary>
    [Description("The SQL query to execute")]
    public string Sql { get; set; }

    /// <summary>
    /// Expected columns in the result.
    /// </summary>
    [Description("Expected column names in the result")]
    public string[] ExpectedColumns { get; set; }
}

