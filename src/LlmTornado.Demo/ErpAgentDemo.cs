using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Samples.ErpAgent;
using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Mcp;

namespace LlmTornado.Demo;

/// <summary>
/// Demo for the ERP SQL Orchestration Agent.
/// Demonstrates querying manufacturing and sales data with automatic correction.
/// </summary>
public class ErpAgentDemo : DemoBase
{
    /// <summary>
    /// Basic ERP agent demo with auto-detected environment.
    /// Set ERP_LOCATION=work or ERP_LOCATION=home to switch databases.
    /// </summary>
    [TornadoTest("ERP Agent - Auto Environment")]
    public static async Task RunErpAgentAuto()
    {
        Console.WriteLine("=== ERP SQL Agent Demo (Auto Environment) ===\n");

        TornadoApi api = Program.Connect();
        ErpAgentOptions options = ErpAgentOptions.FromEnvironment();

        Console.WriteLine($"Database Provider: {options.Provider}");
        Console.WriteLine($"Max Correction Attempts: {options.MaxCorrectionAttempts}\n");

        await RunAgentWithQuestion(api, options, 
            "What were our top 5 selling products last quarter by revenue?");
    }

    /// <summary>
    /// ERP agent demo using MSSQL (work environment).
    /// </summary>
    [TornadoTest("ERP Agent - MSSQL Work")]
    [Flaky("Requires MSSQL connection to VISUAL01/SAN")]
    public static async Task RunErpAgentMsSql()
    {
        Console.WriteLine("=== ERP SQL Agent Demo (MSSQL - Work) ===\n");

        TornadoApi api = Program.Connect();
        ErpAgentOptions options = ErpAgentOptions.ForWork();

        Console.WriteLine($"Host: {options.MsSql?.Host}");
        Console.WriteLine($"Database: {options.MsSql?.Database}\n");

        await RunAgentWithQuestion(api, options,
            "Show me the current inventory levels for our top 10 products by stock value.");
    }

    /// <summary>
    /// ERP agent demo using MySQL (home environment).
    /// </summary>
    [TornadoTest("ERP Agent - MySQL Home")]
    [Flaky("Requires MySQL connection to localhost:3308")]
    public static async Task RunErpAgentMySql()
    {
        Console.WriteLine("=== ERP SQL Agent Demo (MySQL - Home) ===\n");

        TornadoApi api = Program.Connect();
        ErpAgentOptions options = ErpAgentOptions.ForHome();

        Console.WriteLine($"Host: {options.MySql?.Host}:{options.MySql?.Port}");
        Console.WriteLine($"Database: {options.MySql?.Database}\n");

        await RunAgentWithQuestion(api, options,
            "What is the total sales amount by customer for this year?");
    }

    /// <summary>
    /// Interactive ERP agent chatbot with streaming.
    /// </summary>
    [TornadoTest("ERP Agent - Interactive")]
    [Flaky("manual interaction")]
    public static async Task RunErpAgentInteractive()
    {
        Console.WriteLine("=== ERP SQL Agent Interactive Demo ===\n");
        Console.WriteLine("Type 'exit' to quit.\n");

        TornadoApi api = Program.Connect();
        ErpAgentOptions options = ErpAgentOptions.FromEnvironment();

        Console.WriteLine($"Connected to: {options.Provider}");
        Console.WriteLine("Loading MCP tools...\n");

        ErpAgentConfiguration config = new ErpAgentConfiguration(api, options);
        ChatRuntime runtime = new ChatRuntime(config);

        // Subscribe to streaming events
        config.OnRuntimeEvent += (evt) =>
        {
            if (evt is ChatRuntimeAgentRunnerEvents agentEvt)
            {
                if (agentEvt.AgentRunnerEvent is AgentRunnerStreamingEvent streamEvt)
                {
                    if (streamEvt.ModelStreamingEvent is ModelStreamingOutputTextDeltaEvent deltaEvt)
                    {
                        Console.Write(deltaEvt.DeltaText);
                    }
                }
            }
            return ValueTask.CompletedTask;
        };

        Console.WriteLine("Ready! Ask questions about manufacturing or sales data.\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[You]: ");
            Console.ResetColor();

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\n[ERP Agent]: ");
            Console.ResetColor();

            try
            {
                ChatMessage response = await runtime.InvokeAsync(
                    new ChatMessage(ChatMessageRoles.User, input));

                // Response may have been streamed, print final if not
                if (!string.IsNullOrEmpty(response.Content) && !response.Content.StartsWith("[Streamed]"))
                {
                    Console.WriteLine(response.Content);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Demo showing the correction loop in action.
    /// </summary>
    [TornadoTest("ERP Agent - Correction Demo")]
    [Flaky("Requires database connection")]
    public static async Task RunErpAgentWithCorrection()
    {
        Console.WriteLine("=== ERP Agent Correction Loop Demo ===\n");
        Console.WriteLine("This demo uses a complex query that may require correction.\n");

        TornadoApi api = Program.Connect();
        ErpAgentOptions options = ErpAgentOptions.FromEnvironment();
        options.MaxCorrectionAttempts = 3;

        await RunAgentWithQuestion(api, options,
            "Compare our sales performance this quarter vs last quarter, " +
            "broken down by product category, showing percent change.");
    }

    /// <summary>
    /// Helper method to run the agent with a specific question.
    /// </summary>
    private static async Task RunAgentWithQuestion(TornadoApi api, ErpAgentOptions options, string question)
    {
        Console.WriteLine($"Question: {question}\n");
        Console.WriteLine("--- Processing ---\n");

        try
        {
            ErpAgentConfiguration config = new ErpAgentConfiguration(api, options);
            ChatRuntime runtime = new ChatRuntime(config);

            // Track phases
            int phaseCount = 0;
            config.OnRuntimeEvent += (evt) =>
            {
                if (evt is ChatRuntimeAgentRunnerEvents agentEvt)
                {
                    if (agentEvt.AgentRunnerEvent.EventType == AgentRunnerEventTypes.Started)
                    {
                        phaseCount++;
                        Console.WriteLine($"[Phase {phaseCount}] Processing...");
                    }
                }
                return ValueTask.CompletedTask;
            };

            ChatMessage response = await runtime.InvokeAsync(
                new ChatMessage(ChatMessageRoles.User, question));

            Console.WriteLine("\n--- Answer ---\n");
            Console.WriteLine(response.Content);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"\nMake sure the MCP server is installed:");
            Console.WriteLine("  pip install mssql-mcp-server  (for MSSQL)");
            Console.WriteLine("  pip install mysql-mcp-server  (for MySQL)");
            Console.ResetColor();
        }
    }
}

