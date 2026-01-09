using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Planning state: analyzes the user's question and creates a SQL query plan.
/// </summary>
public class ErpPlannerRunnable : OrchestrationRunnable<ChatMessage, SqlQueryPlan>
{
    private readonly TornadoAgent _agent;
    private readonly OrchestrationRuntimeConfiguration _runtime;

    private const string PlannerInstructions = """
        You are an expert ERP data analyst specializing in manufacturing and sales domains.
        
        Given a user question, create a structured plan of SQL queries to answer it.
        
        MANUFACTURING DOMAIN knowledge:
        - Inventory tables (stock levels, locations, movements)
        - Bill of Materials (BOM) - product structures, components
        - Work Orders - production scheduling, status tracking
        - Quality Control - inspections, defects, compliance
        
        SALES DOMAIN knowledge:
        - Customer master data
        - Sales Orders and Order Lines
        - Invoices and Payments
        - Pricing and Discounts
        - Shipping and Delivery
        
        PLANNING GUIDELINES:
        1. Break complex questions into atomic SQL steps
        2. Start with schema exploration if table structure is unclear
        3. Use appropriate JOINs to link related data
        4. Include aggregations (SUM, COUNT, AVG) for summary questions
        5. Add date filters for time-based questions (last quarter, YTD, etc.)
        6. Limit results for top-N questions
        
        Output a structured plan with:
        - TableHints: tables likely needed
        - Steps: ordered SQL queries with descriptions
        - Reasoning: why this plan answers the question
        """;

    public ErpPlannerRunnable(TornadoApi client, OrchestrationRuntimeConfiguration orchestrator, ChatModel? model = null) 
        : base(orchestrator)
    {
        _runtime = orchestrator;

        _agent = new TornadoAgent(
            client: client,
            model: model ?? ChatModel.OpenAi.Gpt4.O,
            name: "ERP Query Planner",
            instructions: PlannerInstructions,
            outputSchema: typeof(SqlQueryPlan)
        );
    }

    public override async ValueTask<SqlQueryPlan> Invoke(RunnableProcess<ChatMessage, SqlQueryPlan> process)
    {
        process.RegisterAgent(_agent);

        // Store original question in runtime properties for later review
        _runtime.RuntimeProperties["OriginalQuestion"] = process.Input.Content ?? "";
        _runtime.RuntimeProperties["CorrectionAttempts"] = 0;

        // Get any existing schema context from previous runs
        string schemaContext = _runtime.RuntimeProperties.TryGetValue("SchemaContext", out object? ctx) 
            ? ctx?.ToString() ?? "No schema context available yet" 
            : "No schema context available yet";

        string prompt = $"""
            User Question: {process.Input.Content}
            
            Available Schema Context:
            {schemaContext}
            
            Create a SQL query plan to answer this question.
            """;

        Conversation conv = await _agent.Run(prompt);

        SqlQueryPlan? plan = conv.Messages.Last().Content?.SmartParseJsonAsync<SqlQueryPlan>(_agent).GetAwaiter().GetResult();

        if (plan is null || plan.Value.Steps is null || plan.Value.Steps.Length == 0)
        {
            // Return a minimal plan that will trigger schema inspection
            return new SqlQueryPlan
            {
                OriginalQuestion = process.Input.Content ?? "",
                TableHints = [],
                Steps = [],
                Reasoning = "Unable to create plan - schema inspection needed"
            };
        }

        // Ensure original question is captured
        SqlQueryPlan result = plan.Value;
        result.OriginalQuestion = process.Input.Content ?? "";
        return result;
    }
}

