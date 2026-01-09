using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Mcp;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Query execution state: executes SQL queries via MCP tools and collects results.
/// </summary>
public class QueryExecutorRunnable : OrchestrationRunnable<SqlQueryPlan, QueryExecutionResult>
{
    private readonly TornadoAgent _agent;
    private readonly MCPServer _mcpServer;
    private readonly OrchestrationRuntimeConfiguration _runtime;

    /// <summary>
    /// Event handler for streaming agent events.
    /// </summary>
    public Action<AgentRunnerEvents>? OnAgentRunnerEvent { get; set; }

    private const string ExecutorInstructions = """
        You are an SQL query executor. Your job is to:
        
        1. Execute the planned SQL queries using the read_query tool
        2. Collect and organize the results
        3. Handle any errors gracefully
        
        EXECUTION GUIDELINES:
        - Execute queries in the order specified
        - If a query fails, note the error and continue with remaining queries
        - Use read_query for SELECT statements
        - Format results clearly showing the data returned
        - Note row counts for each query
        
        After execution, provide a summary of:
        - Which queries succeeded/failed
        - Key data points found
        - Any issues encountered
        """;

    public QueryExecutorRunnable(
        TornadoApi client, 
        MCPServer mcpServer, 
        OrchestrationRuntimeConfiguration orchestrator,
        ChatModel? model = null) 
        : base(orchestrator)
    {
        _mcpServer = mcpServer;
        _runtime = orchestrator;

        _agent = new TornadoAgent(
            client: client,
            model: model ?? ChatModel.OpenAi.Gpt4.O,
            name: "Query Executor",
            instructions: ExecutorInstructions
        );

        // Add MCP SQL tools to the agent
        _agent.AddTool(_mcpServer.AllowedTornadoTools.ToArray());
    }

    public override async ValueTask<QueryExecutionResult> Invoke(RunnableProcess<SqlQueryPlan, QueryExecutionResult> process)
    {
        process.RegisterAgent(_agent);

        SqlQueryPlan plan = process.Input;

        // Get schema context from inspector
        string schemaContext = _runtime.RuntimeProperties.TryGetValue("SchemaContext", out object? ctx)
            ? ctx?.ToString() ?? ""
            : "";

        // Build the query list
        string queryList = plan.Steps?.Length > 0
            ? string.Join("\n", plan.Steps.Select((s, i) => $"{i + 1}. {s.Description}\n   SQL: {s.Sql}"))
            : "No specific queries planned - use schema to construct appropriate queries";

        string prompt = $"""
            Execute SQL queries to answer: {plan.OriginalQuestion}
            
            Schema Context:
            {schemaContext}
            
            Query Plan:
            {queryList}
            
            Execute each query and collect the results. If the planned queries seem incorrect 
            based on the schema, adjust them as needed.
            """;

        Conversation conv = await _agent.Run(
            prompt, 
            maxTurns: 15,
            onAgentRunnerEvent: OnAgentRunnerEvent is not null 
                ? (evt) => { OnAgentRunnerEvent(evt); return ValueTask.CompletedTask; } 
                : null
        );

        string resultData = conv.Messages.Last().Content ?? "";

        // Check for errors in the response
        bool hasErrors = resultData.Contains("error", StringComparison.OrdinalIgnoreCase) 
                      || resultData.Contains("failed", StringComparison.OrdinalIgnoreCase);

        return new QueryExecutionResult
        {
            Plan = plan,
            Data = resultData,
            Success = !hasErrors,
            SchemaContext = schemaContext,
            Errors = hasErrors ? ["Execution encountered issues - see data for details"] : []
        };
    }
}

