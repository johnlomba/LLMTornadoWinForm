## Advanced Orchestration Agents (TornadoAgents / `LlmTornado.Agents`)

This guide is focused on building **advanced AI agents using the orchestration workflow** (the most customizable `ChatRuntime` workflow). It is grounded in the library source plus the samples/demos shipped in this repo.

### What you should already know

- **LLMTornado** (`LlmTornado`): provider-agnostic C# SDK (`TornadoApi`) for chat, responses, embeddings, tools, etc.
- **TornadoAgents** (`LlmTornado.Agents`): agent loop abstractions + `ChatRuntime` workflows.
- **Orchestration workflow**: a state-machine / graph that can run multiple states in parallel and supports conditional transitions.

---

## Architecture overview (where orchestration fits)

### The runtime abstraction: `ChatRuntime` + `IRuntimeConfiguration`

`ChatRuntime` is intentionally thin: it stores an ID and delegates execution to an `IRuntimeConfiguration`. That means your app can always do:

```csharp
ChatRuntime runtime = new ChatRuntime(configuration);
ChatMessage output = await runtime.InvokeAsync(userMessage);
```

…and you can swap the entire agent workflow (singleton, sequential, handoff, orchestration) by swapping the configuration.

### The orchestration configuration: `OrchestrationRuntimeConfiguration`

The most customizable workflow is:

- `OrchestrationRuntimeConfiguration : Orchestration<ChatMessage, ChatMessage>, IRuntimeConfiguration`

It is both:

- a **graph/state-machine runner** (`Orchestration<ChatMessage, ChatMessage>`)
- a **runtime configuration** (`IRuntimeConfiguration`) that knows how to persist conversation history and forward events

It also persists messages via `PersistentConversation` (JSONL file append) so your runtime can resume with history.

---

## The orchestration execution model (tick-based state machine)

### Core concepts

- **Orchestration**: the engine that runs “ticks” until completion/cancellation.
- **Runnable** (`OrchestrationRunnable<TInput, TOutput>`): a node/state that processes one or more processes.
- **Process** (`RunnableProcess<TInput, TOutput>`): a unit of work flowing through the graph.
- **Advancer** (`OrchestrationAdvancer`): a conditional transition from one runnable to the next.

### Tick lifecycle (how a run progresses)

At a high level, orchestration runs in steps:

```mermaid
flowchart TD
  invoke[InvokeAsync(input)] --> init[Initialize(entryRunnable,input)]
  init --> tick[ProcessTick: run all current runnables in parallel]
  tick --> checkpoint[Checkpoint_hook_placeholder]
  checkpoint --> done{IsCompleted_or_Cancelled?}
  done -- yes --> exit[Exit all current runnables]
  done -- no --> advance[SetNewRunnableProcesses: evaluate advancers]
  advance --> exit2[Exit all current runnables]
  exit2 --> setNext[SetCurrentRunnableProcesses: init next runnables]
  setNext --> tick
```

Key details from the library implementation:

- **Parallel runnable execution**: in each tick, all current runnables execute concurrently (`Task.WhenAll`).
- **Transition evaluation**: each runnable evaluates its advancers to produce the next tick’s processes.
- **Exit/cleanup always happens** between ticks: runnables finalize and clear processes while results are captured.

---

## Primitives reference (what to use when)

### `OrchestrationRunnable<TInput, TOutput>` (a state/node)

You implement:

- `public override ValueTask<TOutput> Invoke(RunnableProcess<TInput, TOutput> process)`
- optionally `InitializeRunnable()` and `CleanupRunnable()`

Important behavior you get for free:

- The runnable can have **multiple processes** (inputs) in a tick.
- It records execution timing and token usage into the process (see `RunnableProcess.RegisterAgent(...)`).

### `RunnableProcess` (the unit of work)

Each `RunnableProcess` carries:

- **Input** (`BaseInput` / `Input`)
- **Result** (`BaseResult` / `Result`)
- **Rerun attempts** (`MaxReruns`, `CanReAttempt()`)
- **Metrics**: `StartTime`, `RunnableExecutionTime`, `TokenUsage`

Advanced tip:
- If you call `process.RegisterAgent(agent)`, the process will accumulate `TokenUsage` by listening for `AgentRunnerUsageReceivedEvent`.

### `OrchestrationAdvancer` (a transition)

There are two major transition types:

- **Output-only**: condition checks the source output; input for next runnable is the same output.
- **Converter**: condition checks source output, then converts output into the next runnable’s input type.

Conceptually:

- `out`: \(TOutput -> NextInputType\) must already match (or be assignable)
- `in_out`: \(TOutput -> converter(TOutput) -> NextInputType\)

### Parallelism controls

These flags live on `OrchestrationRunnableBase`:

- **`AllowsParallelAdvances`**:
  - `false` (default): first matching advancer wins (deterministic routing)
  - `true`: all matching advancers fire (fan-out)
- **`SingleInvokeForProcesses`**:
  - `false` (default): invoke per process
  - `true`: invoke once and treat the first process as representative
- **`AllowDeadEnd`**:
  - `false` (default): if no advancer matches, the process may reattempt up to `MaxReruns`
  - `true`: “no outgoing transitions” is allowed (common for background side-effects or terminal states)

---

## Patterns from the shipped samples (copy these, they work)

### Pattern 1: Routing + branch workflows (Selector runnable)

See `LlmTornado.Agents.Samples/ChatBot/MemoryChatBot.cs` and `ChatBot/States/SelectorAgentRunnable.cs`.

Flow:

1. Entry runnable stores the current user task into runtime state (`RuntimeProperties`).
2. A selector runnable uses an LLM to choose the branch:
   - “simple answer”
   - “planning + research pipeline”
3. A converter advancer routes to the right runnable while preserving input.

Why it’s powerful:
- You can keep both a “fast path” and “deep path” in a single agent system.
- You can refine routing decisions with structured output and guardrails.

### Pattern 2: Fan-out context gathering (Parallel advancers)

Fan-out is implemented by:

- setting `fromRunnable.AllowsParallelAdvances = true`
- adding multiple advancers from the same runnable

See:
- `ChatbotAgent.BuildComplexAgent(...)` for a “fan-out” orchestration that gathers web/vector/entity context.
- `MemoryChatBot` for a smaller fan-out that runs selector + vector context in parallel.

Typical fan-out states:
- web search runnable (Response tool)
- vector DB retrieval runnable (ChromaDB/PgVector/etc.)
- entity extraction runnable (structured output)
- passthrough runnable (keeps original user text)

### Pattern 3: Fan-in / join (merge parallel results)

**Important:** the sample `OrchestrationBuilder.AddCombinationalAdvancement(...)` is explicitly a compatibility/demo helper and its join implementation is not production-grade. For a real system, build a join runnable that:

- knows which “signals” it needs
- buffers partial results
- only advances once the required subset is present

There are two approaches:

- **Legacy** (string dictionary state): use `Orchestration.RuntimeProperties` to store partials.
- **Agents 2.0** (typed state): use `IOrchestrationState` and a compiled graph; join state is a strongly typed object.

This guide recommends the typed-state approach for anything beyond demos.

### Pattern 4: Background side-effects (dead-end states)

Persistence and expensive writes (vector save, entity upsert) should be:

- `AllowDeadEnd = true`
- often executed as background tasks inside the runnable to keep chat latency down

See:
- `ChatbotAgent.VectorSaveRunnable` and `VectorEntitySaveRunnable` (Chroma-based examples)
- `ChatBot/States/VectorDataSaverRunnable.cs` (PgVector-based example)

---

## Events, streaming, and observability (UI integration)

### Forward agent streaming/tool events through the runtime

Pattern used in demos/samples:

- attach an `OnAgentRunnerEvent` handler in your runnable
- in `OrchestrationRuntimeConfiguration.OnRuntimeInitialized()`, forward those events into `OnRuntimeEvent`

This enables a UI to listen to one stream (`OnRuntimeEvent`) and render:
- orchestration events (tick, runnable start/end)
- agent runner events (streaming deltas, tool calls, usage)

### Use orchestration events for debugging and progress UI

`OrchestrationRuntimeConfiguration` already forwards orchestration events:
- `OnOrchestrationEvent` → `ChatRuntimeOrchestrationEvent`

Your UI can show:
- which runnable is executing
- when transitions happen
- overall runtime lifecycle (started/completed/cancelled)

---

## Persistence: chat history (how orchestration remembers)

`OrchestrationRuntimeConfiguration` uses `PersistentConversation`:

- loads messages from a JSONL file path (`MessageHistoryFileLocation`)
- appends user and assistant messages each turn
- saves by appending lines (cheap and robust)

Recommended usage:
- configure `MessageHistoryFileLocation` per user/session
- store files in an application data folder
- include a “reset conversation” UX that calls `runtime.Clear()`

---

## Templates (copy/paste starting points)

### Template 1: Minimal orchestration runtime configuration

This is the smallest useful pattern: one agent runnable, with event forwarding.

```csharp
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;

public sealed class MyOrchestrationConfig : OrchestrationRuntimeConfiguration
{
    private readonly MyAgentRunnable _agent;

    public MyOrchestrationConfig(TornadoApi api)
    {
        _agent = new MyAgentRunnable(api, this) { AllowDeadEnd = true };
        SetEntryRunnable(_agent);
        SetRunnableWithResult(_agent);
    }

    public override void OnRuntimeInitialized()
    {
        base.OnRuntimeInitialized();

        _agent.OnAgentRunnerEvent += (evt) =>
        {
            OnRuntimeEvent?.Invoke(new ChatRuntimeAgentRunnerEvents(evt, Runtime?.Id ?? string.Empty));
        };
    }
}

public sealed class MyAgentRunnable : OrchestrationRunnable<ChatMessage, ChatMessage>
{
    private readonly TornadoAgent _agent;
    public Action<AgentRunnerEvents>? OnAgentRunnerEvent { get; set; }

    public MyAgentRunnable(TornadoApi api, Orchestration orchestrator) : base(orchestrator)
    {
        _agent = new TornadoAgent(api, ChatModel.OpenAi.Gpt5.V5Mini, instructions: "You are helpful.", streaming: true);
    }

    public override async ValueTask<ChatMessage> Invoke(RunnableProcess<ChatMessage, ChatMessage> process)
    {
        process.RegisterAgent(_agent);
        Conversation conv = await _agent.Run(
            appendMessages: new List<ChatMessage> { process.Input },
            streaming: _agent.Streaming,
            onAgentRunnerEvent: (evt) => { OnAgentRunnerEvent?.Invoke(evt); return ValueTask.CompletedTask; });

        Orchestrator?.HasCompletedSuccessfully();
        return conv.Messages.Last();
    }
}
```

### Template 2: Fan-out context gathering + routing

Combine these building blocks:

- Entry runnable stores the user request in runtime state
- Parallel fan-out to context sources (vector/web/entity)
- Selector routes to deep or shallow path
- Final agent runnable composes context + question and responds
- Side-effect saver runnable persists results (dead-end)

Use the samples as the concrete reference:
- `LlmTornado.Agents.Samples/ChatBot/MemoryChatBot.cs`
- `LlmTornado.Agents.Samples/ChatBot/ChatbotAgent.cs`

---

## Agents 2.0: strongly-typed state + compiled graphs (recommended for serious orchestrations)

The repo includes a typed-state direction:

- `IOrchestrationState`: marker interface for your orchestration state schema
- `OrchestrationGraphBuilder<TState>`: define nodes + edges
- `OrchestrationGraphCompiler<TState>`: validate + compile
- `ChatRuntime.FromCompiledGraph(compiledGraph)`: run it

### Why it matters (especially for joins)

Legacy `RuntimeProperties`:
- string keys, object values, manual casting
- easy to break and hard to validate

Typed state:
- explicit schema (`class MyState : IOrchestrationState`)
- safer joins (track partial completion in a dictionary/record)
- easier to checkpoint/resume (direction the code is moving toward)

### Typed-state join strategy (recommended pattern)

Use state to buffer parallel results:

- Each context runnable writes into state: `state.Context["web"]=...`, `state.Context["vector"]=...`
- A join runnable checks if required keys are present
- Only then it advances to the final agent runnable

This avoids the “compatibility combinational runnable” limitation and gives you predictable fan-in behavior.

### Template 3: Typed-state orchestration skeleton (compiled graph)

This is the recommended “advanced orchestration” starting point when you need:

- production-grade fan-in joins
- strongly typed cross-state memory
- future checkpointing/resume compatibility (direction the repo is going)

```csharp
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Chat;

// 1) Define your shared state schema
public sealed class MyOrchestrationState : IOrchestrationState
{
    public ChatMessage? LatestUserMessage { get; set; }

    // Fan-in buffer: each context stage writes its output here
    public Dictionary<string, string> Context { get; } = new();
}

// 2) Implement runnables (nodes). Use GetState<TState>() inside Invoke to access typed state.
public sealed class CaptureUserRunnable : OrchestrationRunnable<ChatMessage, ChatMessage>
{
    public CaptureUserRunnable(Orchestration orchestrator) : base(orchestrator) { }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<ChatMessage, ChatMessage> process)
    {
        var state = GetState<MyOrchestrationState>();
        state.LatestUserMessage = process.Input;
        return ValueTask.FromResult(process.Input);
    }
}

public sealed class WebContextRunnable : OrchestrationRunnable<ChatMessage, string>
{
    public WebContextRunnable(Orchestration orchestrator) : base(orchestrator) { AllowDeadEnd = true; }

    public override async ValueTask<string> Invoke(RunnableProcess<ChatMessage, string> process)
    {
        var state = GetState<MyOrchestrationState>();

        // TODO: run a web-search agent/tool here and return a string
        string web = $"WEB: {process.Input.Content}";

        state.Context["web"] = web;
        return web;
    }
}

public sealed class VectorContextRunnable : OrchestrationRunnable<ChatMessage, string>
{
    public VectorContextRunnable(Orchestration orchestrator) : base(orchestrator) { AllowDeadEnd = true; }

    public override async ValueTask<string> Invoke(RunnableProcess<ChatMessage, string> process)
    {
        var state = GetState<MyOrchestrationState>();

        // TODO: query vector DB and return a string
        string vec = $"VEC: {process.Input.Content}";

        state.Context["vector"] = vec;
        return vec;
    }
}

// 3) A real join runnable: only advances once required context keys exist
public sealed class ContextJoinRunnable : OrchestrationRunnable<ChatMessage, ChatMessage>
{
    private readonly string[] _requiredKeys;

    public ContextJoinRunnable(Orchestration orchestrator, params string[] requiredKeys) : base(orchestrator)
    {
        _requiredKeys = requiredKeys.Length == 0 ? new[] { "web", "vector" } : requiredKeys;
        AllowDeadEnd = false;
        MaxReruns = 10; // gives parallel states time to populate state.Context before join gives up
    }

    public override ValueTask<ChatMessage> Invoke(RunnableProcess<ChatMessage, ChatMessage> process)
    {
        var state = GetState<MyOrchestrationState>();

        // If not all required keys are present, emit a “no-op” result and rely on rerun behavior.
        // IMPORTANT: this works best if the join runnable is triggered after fan-out in the graph design.
        bool ready = _requiredKeys.All(k => state.Context.ContainsKey(k));
        if (!ready)
        {
            return ValueTask.FromResult(process.Input);
        }

        string context = string.Join("\n\n", _requiredKeys.Select(k => state.Context[k]));
        string question = state.LatestUserMessage?.Content ?? process.Input.Content ?? "";

        // The next runnable (final agent) can consume this ChatMessage as its input.
        ChatMessage merged = new ChatMessage(process.Input.Role, $"""
Context:
{context}

Question:
{question}
""");

        return ValueTask.FromResult(merged);
    }
}

// 4) Build + compile the graph, then run via ChatRuntime
public static class MyOrchestrationFactory
{
    public static ChatRuntime BuildRuntime()
    {
        var state = new MyOrchestrationState();

        // Create runnables (nodes)
        var capture = new CaptureUserRunnable(orchestrator: null!); // orchestrator is injected at runtime
        var web = new WebContextRunnable(orchestrator: null!);
        var vec = new VectorContextRunnable(orchestrator: null!);
        var join = new ContextJoinRunnable(orchestrator: null!, "web", "vector");

        // Build graph definition
        var graph = new OrchestrationGraphBuilder<MyOrchestrationState>()
            .WithInitialState(state)
            .SetEntryRunnable(capture)
            .SetOutputRunnable(join, withDeadEnd: true)
            // fan-out: capture -> web + vector
            .AddEdge<ChatMessage>(capture, web)
            .AddEdge<ChatMessage>(capture, vec)
            // fan-in: once join is reachable, it can reattempt until state.Context is ready
            .AddEdge<ChatMessage>(capture, join)
            .Build();

        // Compile (validates and prepares execution plan)
        var compiled = new OrchestrationGraphCompiler<MyOrchestrationState>()
            .Compile(graph, state);

        // Create runtime from compiled graph
        return ChatRuntime.FromCompiledGraph(compiled);
    }
}
```

Notes:

- The compiled graph runtime currently wires advancers from edges into the underlying orchestration using a compatibility wrapper, so the orchestration tick semantics still apply (parallel runnable execution, reruns, etc.).
- The `ContextJoinRunnable` above uses reruns as a practical join mechanism. A more explicit join is to wire web/vector into join (with converters) and have join count/process arrivals; typed state makes that deterministic and testable.

---

## Practical checklist for advanced orchestration agents

- **Model contracts**: use structured output for routing/planning stages.
- **Parallel fan-out**: `AllowsParallelAdvances=true` on the fan-out runnable.
- **Fan-in**: implement a real join runnable (prefer typed state).
- **Side effects**: put persistence into dead-end/background states.
- **Observability**: forward both orchestration events and agent runner events to runtime events.
- **Persistence**: store history with `PersistentConversation` per user/session.
- **Safety**: add moderation/guardrails early in the graph (entry runnable).


