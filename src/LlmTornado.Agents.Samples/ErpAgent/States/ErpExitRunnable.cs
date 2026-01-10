using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat;

namespace LlmTornado.Agents.Samples.ErpAgent.States;

/// <summary>
/// Exit state: terminal node that completes the orchestration.
/// </summary>
public class ErpExitRunnable : OrchestrationRunnable<ChatMessage, ChatMessage>
{
    public ErpExitRunnable(Orchestration orchestrator) : base(orchestrator)
    {
        AllowDeadEnd = true; // Terminal node - no further transitions
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<ChatMessage, ChatMessage> process)
    {
        // Simply pass through the response
        return ValueTask.FromResult(process.Input);
    }
}

