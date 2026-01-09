using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Response state: formats the final answer for the user.
/// Supports streaming output.
/// </summary>
public class ResponderRunnable : OrchestrationRunnable<string, ChatMessage>
{
    private readonly TornadoAgent _agent;
    private readonly OrchestrationRuntimeConfiguration _runtime;

    /// <summary>
    /// Event handler for streaming agent events.
    /// </summary>
    public Action<AgentRunnerEvents>? OnAgentRunnerEvent { get; set; }

    private const string ResponderInstructions = """
        You are a helpful ERP data analyst presenting query results to business users.
        
        Your job is to:
        1. Summarize the key findings clearly
        2. Present data in an easy-to-understand format
        3. Highlight important numbers and trends
        4. Answer the original question directly
        
        FORMATTING GUIDELINES:
        - Start with a direct answer to the question
        - Use bullet points for multiple items
        - Use tables for comparative data
        - Include relevant numbers and percentages
        - Note any caveats or limitations
        
        Keep the response professional but accessible to non-technical users.
        """;

    public ResponderRunnable(
        TornadoApi client, 
        OrchestrationRuntimeConfiguration orchestrator,
        ChatModel? model = null) 
        : base(orchestrator)
    {
        _runtime = orchestrator;

        _agent = new TornadoAgent(
            client: client,
            model: model ?? ChatModel.OpenAi.Gpt4.O,
            name: "ERP Analyst",
            instructions: ResponderInstructions,
            streaming: true
        );
    }

    public override async ValueTask<ChatMessage> Invoke(RunnableProcess<string, ChatMessage> process)
    {
        process.RegisterAgent(_agent);

        string data = process.Input;

        string originalQuestion = _runtime.RuntimeProperties.TryGetValue("OriginalQuestion", out object? q)
            ? q?.ToString() ?? ""
            : "";

        int attempts = _runtime.RuntimeProperties.TryGetValue("CorrectionAttempts", out object? attObj)
            ? Convert.ToInt32(attObj)
            : 0;

        string context = attempts > 0 
            ? $"\n\nNote: This answer required {attempts} correction attempt(s) to refine the data."
            : "";

        string prompt = $"""
            ORIGINAL QUESTION: {originalQuestion}
            
            DATA RETRIEVED:
            {data}
            {context}
            
            Please provide a clear, business-friendly answer to the question based on this data.
            """;

        Conversation conv = await _agent.Run(
            prompt,
            streaming: true,
            onAgentRunnerEvent: OnAgentRunnerEvent is not null
                ? (evt) => { OnAgentRunnerEvent(evt); return ValueTask.CompletedTask; }
                : null
        );

        string response = conv.Messages.Last().Content ?? "Unable to generate response.";

        return new ChatMessage(ChatMessageRoles.Assistant, response);
    }
}

