using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Correction state: revises the SQL plan based on review feedback.
/// Implements safeguards to prevent infinite loops.
/// </summary>
public class CorrectorRunnable : OrchestrationRunnable<ReviewResult, SqlQueryPlan>
{
    private readonly TornadoAgent _agent;
    private readonly OrchestrationRuntimeConfiguration _runtime;

    private const string CorrectorInstructions = """
        You are an expert at diagnosing and fixing SQL query issues.
        
        Given a failed query attempt and review feedback, create a corrected SQL plan.
        
        CORRECTION STRATEGIES:
        1. Wrong tables: Find the correct tables using schema context
        2. Wrong columns: Fix column names based on actual schema
        3. Missing joins: Add required JOIN clauses
        4. Wrong filters: Adjust WHERE clauses for correct date ranges, IDs, etc.
        5. Incomplete data: Add additional queries to fill gaps
        6. Wrong aggregation: Fix GROUP BY, SUM, COUNT, etc.
        
        IMPORTANT:
        - Learn from previous errors - don't repeat the same mistakes
        - Use the schema context to ensure correct table/column names
        - Add clear descriptions for each query step
        - Keep the plan focused on answering the original question
        """;

    public CorrectorRunnable(
        TornadoApi client, 
        OrchestrationRuntimeConfiguration orchestrator,
        ChatModel? model = null) 
        : base(orchestrator)
    {
        _runtime = orchestrator;

        _agent = new TornadoAgent(
            client: client,
            model: model ?? ChatModel.OpenAi.Gpt4.O,
            name: "Plan Corrector",
            instructions: CorrectorInstructions,
            outputSchema: typeof(SqlQueryPlan)
        );
    }

    public override async ValueTask<SqlQueryPlan> Invoke(RunnableProcess<ReviewResult, SqlQueryPlan> process)
    {
        process.RegisterAgent(_agent);

        ReviewResult review = process.Input;

        // Get schema context
        string schemaContext = _runtime.RuntimeProperties.TryGetValue("SchemaContext", out object? ctx)
            ? ctx?.ToString() ?? ""
            : "";

        string originalQuestion = _runtime.RuntimeProperties.TryGetValue("OriginalQuestion", out object? q)
            ? q?.ToString() ?? review.ExecutionResult.Plan.OriginalQuestion
            : review.ExecutionResult.Plan.OriginalQuestion;

        // Build previous plan summary
        string previousPlan = review.ExecutionResult.Plan.Steps?.Length > 0
            ? string.Join("\n", review.ExecutionResult.Plan.Steps.Select(s => $"- {s.Description}: {s.Sql}"))
            : "No previous plan";

        string prompt = $"""
            ORIGINAL QUESTION: {originalQuestion}
            
            PREVIOUS PLAN (FAILED):
            {previousPlan}
            
            EXECUTION RESULT:
            {review.ExecutionResult.Data}
            
            REVIEW ISSUES:
            {string.Join("\n", review.Issues.Select(i => $"- {i}"))}
            
            CORRECTION HINT:
            {review.CorrectionHint}
            
            SCHEMA CONTEXT:
            {schemaContext}
            
            ATTEMPT: {review.Attempts + 1}
            
            Create a corrected SQL plan that addresses these issues.
            """;

        Conversation conv = await _agent.Run(prompt);

        SqlQueryPlan? plan = conv.Messages.Last().Content?.SmartParseJsonAsync<SqlQueryPlan>(_agent).GetAwaiter().GetResult();

        if (plan is null || plan.Value.Steps is null || plan.Value.Steps.Length == 0)
        {
            // Fallback: return modified previous plan
            return new SqlQueryPlan
            {
                OriginalQuestion = originalQuestion,
                TableHints = review.ExecutionResult.Plan.TableHints,
                Steps = review.ExecutionResult.Plan.Steps ?? [],
                Reasoning = $"Correction attempt {review.Attempts + 1}: {review.CorrectionHint}"
            };
        }

        SqlQueryPlan result = plan.Value;
        result.OriginalQuestion = originalQuestion;
        return result;
    }
}

