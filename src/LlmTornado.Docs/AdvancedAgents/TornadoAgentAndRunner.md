## TornadoAgent + TornadoRunner (Agent Loop) Guide

This document focuses only on **setting up `TornadoAgent`** and **running it via `TornadoRunner`** (directly or via `TornadoAgent.Run(...)`). It does not cover `ChatRuntime` orchestration workflows.

Grounded in:

- `LlmTornado.Agents/TornadoAgent.cs`
- `LlmTornado.Agents/TornadoRunner.cs`
- `LlmTornado.Agents/ToolRunner.cs`
- Website docs: `LlmTornado.Docs/website/docs/2. Agents/2. Tornado-Agent/*`

---

## What is what (quick mental model)

- **`TornadoAgent`**: configuration + convenience wrapper around the agent loop.
  - Holds model, instructions, tool list, output schema, request options, and event hooks.
- **`TornadoRunner`**: the actual loop implementation.
  - Executes: prompt → model → tool calls → tool results → model → … until done.

Important:
- Calling `agent.Run(...)` ends up calling **`TornadoRunner.RunAsync(agent, ...)`** internally.
- You call `TornadoRunner` directly when you want to run the loop without using agent convenience overloads, or when building higher-level wrappers.

---

## 1) Create a `TornadoAgent`

Minimal setup:

```csharp
using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Chat.Models;

TornadoApi api = new TornadoApi(Environment.GetEnvironmentVariable("OPENAI_API_KEY"), LLmProviders.OpenAi);

TornadoAgent agent = new TornadoAgent(
    client: api,
    model: ChatModel.OpenAi.Gpt5.V5Mini,
    name: "Assistant",
    instructions: "You are a helpful assistant."
);
```

### Agent fields you will commonly use

- **`agent.Options`** (`ChatRequest`): controls request settings (model already set; tools are injected per-run).
- **`agent.ResponseOptions`** (`ResponseRequest?`): for Response API tool sets (e.g., web search tool).
- **`agent.Streaming`**: default streaming behavior for this agent.
- **`agent.ToolList` / `agent.McpTools`**: tool registry maps.

---

## 2) Add tools (function calling)

### Add tools via delegates in constructor

```csharp
using System.ComponentModel;

public static class WeatherTools
{
    [Description("Get the current weather in a given location")]
    public static string GetWeather([Description("City, e.g. Boston")] string location)
        => $"Weather in {location}: Sunny";
}

TornadoAgent agent = new TornadoAgent(
    api,
    ChatModel.OpenAi.Gpt5.V5Mini,
    instructions: "You are a helpful assistant. Use tools when needed.",
    tools: [WeatherTools.GetWeather]
);
```

### Tool permission gating

`TornadoRunner` will call your approval callback when a tool is marked as requiring permission.

```csharp
agent.ToolPermissionRequired["GetWeather"] = true;

ValueTask<bool> approve(string prompt)
{
    Console.WriteLine(prompt);
    return ValueTask.FromResult(true); // replace with real approval UX
}
```

Then pass `toolPermissionHandle: approve` when running (see run section).

### Tool result post-processing (highly recommended)

Use `agent.ToolResultProcessor` to shrink/redact tool output before it returns to the model:

```csharp
agent.ToolResultProcessor = (toolName, functionResult, functionCall) =>
{
    if (functionResult?.Content is string s && s.Length > 5_000)
+   {
        functionResult.Content = s.Substring(0, 5_000) + "\n[TRUNCATED]";
    }
    return ValueTask.CompletedTask;
};
```

This is the best place to enforce:
- max size limits
- redaction
- normalization (e.g., always JSON)

---

## 3) Structured output (schema-driven responses)

If you provide an output schema type, the agent configures response formatting:

```csharp
public struct Classification
{
    public string Label { get; set; }
    public string Reason { get; set; }
}

TornadoAgent agent = new TornadoAgent(
    api,
    ChatModel.OpenAi.Gpt5.V5Mini,
    instructions: "Classify user requests.",
    outputSchema: typeof(Classification)
);
```

At runtime, parse `result.Messages.Last().Content` into your type (helpers exist in the repo; usage varies by sample).

### Changing schema dynamically

`TornadoAgent.UpdateOutputSchema(newSchema)` updates response formatting for future runs.

---

## 4) Run the agent (the loop)

### Option A: Run via the agent wrapper (`agent.Run(...)`)

This is the most common (it forwards to `TornadoRunner` internally):

```csharp
Conversation result = await agent.Run(
    input: "What is 2+2?",
    maxTurns: 10
);
Console.WriteLine(result.Messages.Last().Content);
```

Key parameters on `agent.Run(...)` (these map into `TornadoRunner`):

- **`input`**: string or `List<ChatMessagePart>`
- **`appendMessages`**: conversation history you want injected (system messages are skipped by runner to avoid duplicating instructions)
- **`inputGuardRailFunction`**: pre-run guardrail (see below)
- **`streaming`** + **`onAgentRunnerEvent`**: streaming requires handling events to see output deltas
- **`maxTurns`**: caps tool loop iterations
- **`responseId`**: use Response API thread continuation (changes how instructions are handled)
- **`toolPermissionHandle`**: tool approval callback
- **`singleTurn`**: debugging mode (don’t keep looping tool calls)
- **`runnerOptions`**: token limit / throw behavior
- **`cancellationToken`**: hard cancellation

### Option B: Run directly via `TornadoRunner`

Useful when you don’t want to call instance methods, or you’re building your own wrappers:

```csharp
Conversation result = await TornadoRunner.RunAsync(
    agent,
    input: "Hello",
    maxTurns: 10,
    streaming: false
);
```

---

## 5) Streaming + events

### Streaming requires an event handler

Set streaming either on the agent (`agent.Streaming = true`) or per-run (`streaming: true`), and provide `onAgentRunnerEvent`.

```csharp
agent.Streaming = true;

ValueTask onEvent(AgentRunnerEvents e)
{
    if (e is AgentRunnerStreamingEvent se &&
        se.ModelStreamingEvent is ModelStreamingOutputTextDeltaEvent delta)
    {
        Console.Write(delta.DeltaText);
    }
    return ValueTask.CompletedTask;
}

await agent.Run(
    input: "Tell me a story.",
    streaming: true,
    onAgentRunnerEvent: onEvent
);
```

### Important event types you’ll typically handle

- `Started`, `Completed`, `Error`
- `ToolInvoked`, `ToolCompleted`
- `Streaming` (text deltas + other model streaming events)
- `UsageReceived` (tokens)
- `MaxTurnsReached`, `MaxTokensReached`, `Cancelled`, `GuardRailTriggered`

---

## 6) Guardrails (stop execution before it starts)

Guardrails are implemented as a function that returns `GuardRailFunctionOutput`.

```csharp
public struct IsMath
{
    public string Reasoning { get; set; }
    public bool IsMathRequest { get; set; }
}

async ValueTask<GuardRailFunctionOutput> MathGuardRail(string? input = "")
{
    TornadoAgent guard = new TornadoAgent(
        api,
        ChatModel.OpenAi.Gpt5.V5Mini,
        instructions: "Check if the user is asking a math question.",
        outputSchema: typeof(IsMath)
    );

    Conversation r = await TornadoRunner.RunAsync(guard, input);
    IsMath? parsed = r.Messages.Last().Content.JsonDecode<IsMath>();
    return new GuardRailFunctionOutput(parsed?.Reasoning ?? "", tripwireTriggered: !(parsed?.IsMathRequest ?? false));
}

Conversation result = await agent.Run(
    input: "What is the weather?",
    inputGuardRailFunction: MathGuardRail
);
```

If triggered, the runner emits a `GuardRailTriggered` event (if you provided an event handler) and may throw `GuardRailTriggerException` depending on how you handle it.

---

## 7) Response API tool sets (`agent.ResponseOptions`)

`TornadoRunner` injects tools from both:
- `agent.Options.Tools` (normal function calling tools)
- `agent.ResponseOptions.Tools` (Response API tool set, e.g. web search tool)

Example (web search tool):

```csharp
agent.ResponseOptions = new ResponseRequest
{
    Tools = [ new ResponseWebSearchTool() ]
};
```

---

## 8) Runner controls for production hardening (`TornadoRunnerOptions`)

Use `runnerOptions` to enforce limits:

- **`TokenLimit`**: stop/throw if context grows too large
- **`ThrowOnMaxTurnsExceeded`**
- **`ThrowOnTokenLimitExceeded`**
- **`ThrowOnCancelled`**

```csharp
var opts = new TornadoRunnerOptions
{
    TokenLimit = 200_000,
    ThrowOnTokenLimitExceeded = true
};

Conversation result = await agent.Run(
    input: "Do something complex",
    runnerOptions: opts
);
```

---

## 9) Common gotchas (specific to this implementation)

- **Streaming does not magically print**: you must handle `AgentRunnerStreamingEvent` to display deltas.
- **Tool calls are executed in parallel (non-streaming path)**: if your tools share mutable state, protect it.
- **`appendMessages` system messages are skipped** by the runner to avoid instruction overlap. Don’t rely on appending system messages for behavior; use `agent.Instructions`.
- **`responseId` changes instruction handling**: when `responseId` is set, the runner sets “previous response id” instead of adding the system instruction message.
- **Tool permission map must include the tool key**: the runner indexes `agent.ToolPermissionRequired[toolCall.Name]`. If you register tools via `agent.AddTool(...)`, the agent adds default permission entries; if you manipulate tool maps manually, ensure the key exists.

---

## Where to look next (code)

- `LlmTornado.Agents/TornadoAgent.cs`: agent configuration surface and `Run(...)` wrapper
- `LlmTornado.Agents/TornadoRunner.cs`: loop semantics, tool handling, streaming handling
- `LlmTornado.Agents/ToolRunner.cs`: function tool invocation + MCP invocation + tool result processor


