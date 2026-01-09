using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Samples.ErpAgent.DataModels;
using LlmTornado.Agents.Samples.ErpAgent.States;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Mcp;

namespace LlmTornado.Agents.Samples.ErpAgent;

/// <summary>
/// Orchestration configuration for the ERP SQL Agent.
/// 
/// Workflow:
/// 1. Planner - Analyzes question, creates SQL plan
/// 2. SchemaInspector - Queries DB schema via MCP
/// 3. QueryExecutor - Executes SQL queries via MCP
/// 4. Reviewer - Validates results
/// 5. Corrector - Revises plan if needed (max 3 attempts)
/// 6. Responder - Formats final answer
/// </summary>
public class ErpAgentConfiguration : OrchestrationRuntimeConfiguration
{
    private readonly MCPServer _mcpServer;
    private readonly ErpPlannerRunnable _planner;
    private readonly SchemaInspectorRunnable _inspector;
    private readonly QueryExecutorRunnable _executor;
    private readonly ReviewerRunnable _reviewer;
    private readonly CorrectorRunnable _corrector;
    private readonly ResponderRunnable _responder;
    private readonly ErpExitRunnable _exit;

    /// <summary>
    /// The TornadoApi client used by this agent.
    /// </summary>
    public TornadoApi Client { get; }

    /// <summary>
    /// The MCP server providing SQL tools.
    /// </summary>
    public MCPServer McpServer => _mcpServer;

    /// <summary>
    /// Creates a new ERP agent configuration.
    /// </summary>
    /// <param name="api">TornadoApi client for LLM calls</param>
    /// <param name="options">Database connection options</param>
    /// <param name="model">Optional: override the default model (GPT-4o)</param>
    public ErpAgentConfiguration(TornadoApi api, ErpAgentOptions options, ChatModel? model = null)
    {
        Client = api;
        RecordSteps = true;

        // Create MCP server based on provider
        _mcpServer = options.Provider switch
        {
            DatabaseProvider.MsSql => MCPToolkits.MsSql(options.MsSql 
                ?? throw new ArgumentException("MsSql options required when Provider is MsSql")),
            DatabaseProvider.MySql => MCPToolkits.MySql(options.MySql 
                ?? throw new ArgumentException("MySql options required when Provider is MySql")),
            _ => throw new ArgumentException($"Unknown database provider: {options.Provider}")
        };

        // Initialize MCP server (fetches available tools)
        _mcpServer.InitializeAsync().GetAwaiter().GetResult();

        // Create state runnables
        _planner = new ErpPlannerRunnable(api, this, model);
        _inspector = new SchemaInspectorRunnable(api, _mcpServer, this, model);
        _executor = new QueryExecutorRunnable(api, _mcpServer, this, model);
        _reviewer = new ReviewerRunnable(api, this, options.MaxCorrectionAttempts, model);
        _corrector = new CorrectorRunnable(api, this, model);
        _responder = new ResponderRunnable(api, this, model);
        _exit = new ErpExitRunnable(this) { AllowDeadEnd = true };

        // Wire the orchestration graph
        WireOrchestration(options.MaxCorrectionAttempts);
    }

    private void WireOrchestration(int maxAttempts)
    {
        // Main flow: Planner -> Inspector -> Executor -> Reviewer
        _planner.AddAdvancer(_inspector);
        _inspector.AddAdvancer(_executor);
        _executor.AddAdvancer(_reviewer);

        // Review branching
        // Success path: Reviewer -> Responder -> Exit
        _reviewer.AddAdvancer<string>(
            r => r.Success,
            r => r.Data,
            _responder
        );

        // Correction path: Reviewer -> Corrector -> Executor (loop back)
        // Only if not successful AND attempts < max
        _reviewer.AddAdvancer<ReviewResult>(
            r => !r.Success && r.Attempts < maxAttempts,
            r => r,  // Pass full ReviewResult to corrector
            _corrector
        );

        // Give up path: Reviewer -> Responder (with partial data)
        // When max attempts reached
        _reviewer.AddAdvancer<string>(
            r => !r.Success && r.Attempts >= maxAttempts,
            r => r.PartialData,
            _responder
        );

        // Corrector loops back to executor with revised plan
        _corrector.AddAdvancer(_executor);

        // Responder -> Exit
        _responder.AddAdvancer(_exit);

        // Set entry and result points
        SetEntryRunnable(_planner);
        SetRunnableWithResult(_responder);
    }

    /// <summary>
    /// Called when the runtime is initialized.
    /// Sets up event forwarding for streaming.
    /// </summary>
    public override void OnRuntimeInitialized()
    {
        base.OnRuntimeInitialized();

        // Forward executor events to runtime
        _executor.OnAgentRunnerEvent += (evt) =>
        {
            OnRuntimeEvent?.Invoke(new ChatRuntimeAgentRunnerEvents(evt, Runtime?.Id ?? string.Empty));
        };

        // Forward responder events to runtime (for streaming response)
        _responder.OnAgentRunnerEvent += (evt) =>
        {
            OnRuntimeEvent?.Invoke(new ChatRuntimeAgentRunnerEvents(evt, Runtime?.Id ?? string.Empty));
        };
    }
}

