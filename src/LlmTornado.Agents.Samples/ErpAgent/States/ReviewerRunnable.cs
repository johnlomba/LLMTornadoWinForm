using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Review state: validates execution results against the original question.
/// Decides whether to proceed to response or trigger correction.
/// </summary>
public class ReviewerRunnable : OrchestrationRunnable<QueryExecutionResult, ReviewResult>
{
    private readonly TornadoAgent _agent;
    private readonly OrchestrationRuntimeConfiguration _runtime;
    private readonly int _maxAttempts;

    private const string ReviewerInstructions = """
        You are a quality assurance reviewer for SQL query results.
        
        Your job is to verify that the execution results adequately answer the original question.
        
        REVIEW CRITERIA:
        1. Relevance: Does the data actually answer what was asked?
        2. Completeness: Are all parts of the question addressed?
        3. Correctness: Does the data look reasonable (no obvious errors)?
        4. Data Quality: Are there unexpected nulls, wrong types, or suspicious values?
        
        CONFIDENCE SCORING:
        - 90-100: Excellent - data clearly and completely answers the question
        - 70-89: Good - data answers the question with minor gaps
        - 50-69: Partial - some relevant data but significant gaps
        - 0-49: Poor - data does not adequately answer the question
        
        If confidence < 70, provide specific correction hints.
        """;

    public ReviewerRunnable(
        TornadoApi client, 
        OrchestrationRuntimeConfiguration orchestrator,
        int maxAttempts = 3,
        ChatModel? model = null) 
        : base(orchestrator)
    {
        _runtime = orchestrator;
        _maxAttempts = maxAttempts;

        _agent = new TornadoAgent(
            client: client,
            model: model ?? ChatModel.OpenAi.Gpt4.O,
            name: "Results Reviewer",
            instructions: ReviewerInstructions,
            outputSchema: typeof(ReviewVerdict)
        );
    }

    public override async ValueTask<ReviewResult> Invoke(RunnableProcess<QueryExecutionResult, ReviewResult> process)
    {
        process.RegisterAgent(_agent);

        QueryExecutionResult execution = process.Input;

        // Get current attempt count
        int attempts = _runtime.RuntimeProperties.TryGetValue("CorrectionAttempts", out object? attObj)
            ? Convert.ToInt32(attObj)
            : 0;

        string originalQuestion = _runtime.RuntimeProperties.TryGetValue("OriginalQuestion", out object? q)
            ? q?.ToString() ?? execution.Plan.OriginalQuestion
            : execution.Plan.OriginalQuestion;

        string prompt = $"""
            ORIGINAL QUESTION: {originalQuestion}
            
            EXECUTION RESULTS:
            {execution.Data}
            
            EXECUTION SUCCESS: {execution.Success}
            ERRORS: {string.Join(", ", execution.Errors)}
            
            Review these results and provide your verdict.
            """;

        Conversation conv = await _agent.Run(prompt);

        ReviewVerdict? verdict = conv.Messages.Last().Content?.SmartParseJsonAsync<ReviewVerdict>(_agent).GetAwaiter().GetResult();

        bool success = verdict?.AnswersQuestion == true 
                    && verdict?.DataLooksCorrect == true 
                    && verdict?.ConfidenceScore >= 70;

        ReviewResult result = new()
        {
            Success = success,
            ExecutionResult = execution,
            Data = execution.Data,
            PartialData = execution.Data, // Use as partial if we give up
            Attempts = attempts,
            Issues = verdict?.Issues?.ToList() ?? [],
            CorrectionHint = verdict?.CorrectionHint ?? ""
        };

        // If not successful and we have attempts left, prepare correction plan
        if (!success && attempts < _maxAttempts)
        {
            result.CorrectionPlan = new SqlQueryPlan
            {
                OriginalQuestion = originalQuestion,
                TableHints = execution.Plan.TableHints,
                Steps = [], // Corrector will fill this
                Reasoning = $"Correction needed: {verdict?.CorrectionHint}"
            };

            // Increment attempt counter
            _runtime.RuntimeProperties["CorrectionAttempts"] = attempts + 1;
        }

        return result;
    }
}

