using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Mcp;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Schema inspection state: queries database schema and sample data via MCP tools.
/// Enriches the query plan with actual table/column information.
/// </summary>
public class SchemaInspectorRunnable : OrchestrationRunnable<SqlQueryPlan, SqlQueryPlan>
{
    private readonly TornadoAgent _agent;
    private readonly MCPServer _mcpServer;
    private readonly OrchestrationRuntimeConfiguration _runtime;

    private const string InspectorInstructions = """
        You are a database schema inspector. Your job is to:
        
        1. Use the list_tables tool to see what tables are available
        2. Use describe_table to get column details for relevant tables
        3. Optionally use read_query with LIMIT to see sample data
        
        Based on the query plan provided, inspect the schema to:
        - Verify the hinted tables exist
        - Find the correct column names
        - Identify any joins needed
        - Note data types for proper filtering
        
        After inspection, output a summary of what you found.
        """;

    public SchemaInspectorRunnable(
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
            name: "Schema Inspector",
            instructions: InspectorInstructions
        );

        // Add MCP SQL tools to the agent
        _agent.AddTool(_mcpServer.AllowedTornadoTools.ToArray());
    }

    public override async ValueTask<SqlQueryPlan> Invoke(RunnableProcess<SqlQueryPlan, SqlQueryPlan> process)
    {
        process.RegisterAgent(_agent);

        SqlQueryPlan plan = process.Input;

        string tableHints = plan.TableHints?.Length > 0 
            ? string.Join(", ", plan.TableHints) 
            : "unknown - please discover";

        string prompt = $"""
            I need to answer this question: {plan.OriginalQuestion}
            
            Table hints from planning: {tableHints}
            
            Please:
            1. List available tables
            2. Describe the relevant tables for this query
            3. Note key columns, data types, and relationships
            
            Provide a schema summary I can use to refine the SQL queries.
            """;

        Conversation conv = await _agent.Run(prompt, maxTurns: 8);

        // Store schema context for future use
        string schemaContext = conv.Messages.Last().Content ?? "";
        _runtime.RuntimeProperties["SchemaContext"] = schemaContext;

        // Return the enriched plan (the executor will use the schema context)
        return plan;
    }
}

