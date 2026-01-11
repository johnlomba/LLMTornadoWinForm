using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using LlmTornado;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;

namespace LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;

/// <summary>
/// Runtime configuration that automatically compresses long chats by summarizing
/// older context into ordered assistant messages while preserving the system prompt.
/// Tool calls are stripped; tool activity is summarized as plain text.
/// </summary>
public class ChatUIRuntimeConfiguration : IRuntimeConfiguration
{
    private const double TriggerFraction = 0.60;          // Summarize when total tokens exceed 60% of max
    private const double TargetFraction = 0.25;           // Compress older context to ~25% of max
    private const double LargeKeepFraction = 0.20;        // If a kept message exceeds 20% of max, include it in summary
    private const int FallbackMaxTokens = 16_000;         // Default context window when model doesn't specify
    private const int MinChunkTokens = 200;               // Lower bound for chunk target size
    private const int MaxToolsPerRun = 20;                // Cap on tools passed to the model
    private const int ContextMessagesToScan = 8;          // How many recent messages to inspect for tool relevance
    private const int ContextCharsCap = 4000;             // Cap context text to avoid huge scans

    private readonly TornadoApi _client;
    private readonly ChatModel _model;
    private readonly string _name;
    private readonly string _instructions;
    private readonly Type? _outputSchema;
    private readonly bool _streaming;
    private readonly ChatRequest _baseRequest;
    private readonly List<Tool> _availableTools;
    private readonly Dictionary<string, bool>? _toolPermissionRequired;

    public ChatRuntime Runtime { get; set; }
    public Func<ChatRuntimeEvents, ValueTask>? OnRuntimeEvent { get; set; }
    public Func<string, ValueTask<bool>>? OnRuntimeRequestEvent { get; set; }
    public CancellationTokenSource cts { get; set; } = new CancellationTokenSource();

    /// <summary>
    /// Current conversation managed by the runtime.
    /// </summary>
    public Conversation Conversation { get; set; }

    /// <summary>
    /// Initializes the runtime with a base model/client and a pool of available tools.
    /// Tools are filtered per-message (max 20) based on recent context before each run.
    /// </summary>
    public ChatUIRuntimeConfiguration(
        TornadoApi client,
        ChatModel model,
        IEnumerable<Tool> availableTools,
        string name = "Assistant",
        string instructions = "You are a helpful assistant",
        Type? outputSchema = null,
        bool streaming = false,
        ChatRequest? requestTemplate = null,
        Dictionary<string, bool>? toolPermissionRequired = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _name = string.IsNullOrWhiteSpace(name) ? "Assistant" : name;
        _instructions = string.IsNullOrWhiteSpace(instructions) ? "You are a helpful assistant" : instructions;
        _outputSchema = outputSchema;
        _streaming = streaming;
        _availableTools = availableTools?.ToList() ?? [];
        _toolPermissionRequired = toolPermissionRequired;

        ChatRequest template = requestTemplate is null
            ? new ChatRequest { Model = model }
            : new ChatRequest(requestTemplate);

        // Clear tools on the template; we will inject a contextually selected subset each run.
        template.Tools = [];

        _baseRequest = template;
        Conversation = client.Chat.CreateConversation(_baseRequest);
    }

    public void OnRuntimeInitialized()
    {
    }

    public void CancelRuntime()
    {
        cts.Cancel();
        OnRuntimeEvent?.Invoke(new ChatRuntimeCancelledEvent(Runtime.Id));
    }

    public async ValueTask<ChatMessage> AddToChatAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        OnRuntimeEvent?.Invoke(new ChatRuntimeStartedEvent(Runtime.Id));

        Conversation.AppendMessage(message);

        await SummarizeIfNeededAsync(cancellationToken);

        TornadoAgent runAgent = await BuildAgentForContextAsync(cancellationToken);

        Conversation = await runAgent.Run(
            appendMessages: Conversation.Messages.ToList(),
            streaming: runAgent.Streaming,
            onAgentRunnerEvent: (sEvent) =>
            {
                OnRuntimeEvent?.Invoke(new ChatRuntimeAgentRunnerEvents(sEvent, Runtime.Id));
                return Threading.ValueTaskCompleted;
            },
            toolPermissionHandle: OnRuntimeRequestEvent,
            cancellationToken: cancellationToken
        );

        OnRuntimeEvent?.Invoke(new ChatRuntimeCompletedEvent(Runtime.Id));
        return Conversation?.Messages.LastOrDefault() ?? new ChatMessage();
    }

    public ChatMessage GetLastMessage()
    {
        return Conversation?.Messages.LastOrDefault() ?? new ChatMessage();
    }

    public List<ChatMessage> GetMessages()
    {
        return Conversation?.Messages.ToList() ?? [];
    }

    public void ClearMessages()
    {
        Conversation?.Clear();
    }

    private async Task<TornadoAgent> BuildAgentForContextAsync(CancellationToken cancellationToken)
    {
        List<Tool> selectedTools = await SelectToolsWithLlmAsync(cancellationToken);
        ChatRequest request = new ChatRequest(_baseRequest);
        TornadoAgent agent = new TornadoAgent(
            _client,
            _model,
            _name,
            _instructions,
            _outputSchema,
            tools: null,
            _streaming,
            _toolPermissionRequired,
            request);
        agent.AddTool(selectedTools);
        return agent;
    }

    private async Task<List<Tool>> SelectToolsWithLlmAsync(CancellationToken cancellationToken)
    {
        if (_availableTools.Count <= MaxToolsPerRun)
        {
            return _availableTools.ToList();
        }

        string contextText = GetContextSlice();
        string toolList = string.Join("\n", _availableTools.Select(t =>
        {
            string name = t.ToolName ?? t.Function?.Name ?? t.Custom?.Name ?? "tool";
            string desc = t.ToolDescription ?? t.Function?.Description ?? t.Custom?.Description ?? string.Empty;
            return $"- {name}: {desc}";
        }));

        string[] toolNames = _availableTools
            .Select(t => t.ToolName ?? t.Function?.Name ?? t.Custom?.Name ?? "tool")
            .ToArray();

        ChatRequestResponseFormats responseFormat = BuildToolSelectionResponseFormat(toolNames);

        TornadoAgent selector = new TornadoAgent(_client, _model, name: "ToolSelector", instructions: "Select the most relevant tools for the next assistant turn.", options: new ChatRequest { Model = _model, Tools = [], ResponseFormat = responseFormat });

        string prompt =
            $"Conversation context (truncated):\n{contextText}\n\nAvailable tools:\n{toolList}\n\n" +
            $"Select up to {MaxToolsPerRun} tools. Return only per the structured schema.";

        Conversation selectionConv = await selector.Run(prompt, cancellationToken: cancellationToken);

        string? response = selectionConv.Messages.LastOrDefault()?.Content;
        if (string.IsNullOrWhiteSpace(response))
        {
            return _availableTools.Take(MaxToolsPerRun).ToList();
        }
        
        List<string> names = ParseSelectedTools(response);

        if (names.Count == 0)
        {
            return _availableTools.Take(MaxToolsPerRun).ToList();
        }

        HashSet<string> nameSet = new HashSet<string>(
            names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);

        List<Tool> selected = _availableTools
            .Where(t =>
            {
                string n = t.ToolName ?? t.Function?.Name ?? t.Custom?.Name ?? string.Empty;
                return nameSet.Contains(n);
            })
            .Take(MaxToolsPerRun)
            .ToList();

        if (selected.Count == 0)
        {
            return _availableTools.Take(MaxToolsPerRun).ToList();
        }

        return selected;
    }

    private string GetContextSlice()
    {
        List<ChatMessage> messages = Conversation.Messages.ToList();
        IEnumerable<ChatMessage> sample = messages.Count <= ContextMessagesToScan
            ? messages
            : messages.Skip(messages.Count - ContextMessagesToScan);
        string text = string.Join(" ", sample.Select(m => m.GetMessageContent()));
        if (text.Length > ContextCharsCap)
        {
            text = text.Substring(text.Length - ContextCharsCap, ContextCharsCap);
        }

        return text;
    }

    private static ChatRequestResponseFormats BuildToolSelectionResponseFormat(string[] toolNames)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                tools = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            reason = new
                            {
                                type = "string",
                                description = "Reason for selecting the tool"
                            },
                            tool = new
                            {
                                type = "string",
                                description = "Tool to use next",
                                // use @ to allow enum keyword
                                @enum = toolNames
                            }
                        },
                        required = new[] { "reason", "tool" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "tools" },
            additionalProperties = false
        };

        return ChatRequestResponseFormats.StructuredJson("tool_selection", schema, "Select relevant tools", true);
    }

    private static List<string> ParseSelectedTools(string response)
    {
        List<string> selected = [];
        try
        {
            using JsonDocument doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tools", out JsonElement toolsArray))
            {
                foreach (JsonElement item in toolsArray.EnumerateArray())
                {
                    if (item.TryGetProperty("tool", out JsonElement toolNameElement))
                    {
                        string? name = toolNameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            selected.Add(name);
                        }
                    }
                }
            }
        }
        catch
        {
            // fall back to empty => caller will use fallback selection
        }

        return selected;
    }

    private static ChatRequestResponseFormats BuildSummaryResponseFormat()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                summaries = new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        description = "One assistant summary message in chronological order"
                    },
                    minItems = 1,
                    maxItems = 20
                }
            },
            required = new[] { "summaries" },
            additionalProperties = false
        };

        return ChatRequestResponseFormats.StructuredJson("summaries", schema, "Ordered assistant summary messages", true);
    }

    private static List<string> ParseSummaries(string response)
    {
        List<string> summaries = [];
        try
        {
            using JsonDocument doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("summaries", out JsonElement arr))
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string? text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        summaries.Add(text.Trim());
                    }
                }
            }
        }
        catch
        {
            // fallback to empty; caller will handle
        }

        return summaries;
    }

    private async Task SummarizeIfNeededAsync(CancellationToken cancellationToken)
    {
        if (Conversation.Messages.Count == 0)
        {
            return;
        }

        int maxTokens = GetMaxContextTokens();
        if (maxTokens <= 0)
        {
            maxTokens = FallbackMaxTokens;
        }

        List<ChatMessage> allMessages = Conversation.Messages.ToList();
        int totalTokens = allMessages.Sum(m => m.GetMessageTokens());

        if (totalTokens <= maxTokens * TriggerFraction)
        {
            return;
        }

        int allowedSummaryTokens = (int)(maxTokens * TargetFraction);

        List<ChatMessage> systemMessages = allMessages.Where(m => m.Role == ChatMessageRoles.System).ToList();
        List<ChatMessage> nonSystem = allMessages.Where(m => m.Role != ChatMessageRoles.System).ToList();

        List<ChatMessage> keptRecent = GetRecentMessages(nonSystem, maxTokens);

        // Remove tool payloads from the kept set to satisfy "remove tool calls completely"
        keptRecent = keptRecent.Where(m => !IsToolPayload(m)).ToList();

        List<ChatMessage> summarizable = nonSystem
            .Where(m => !keptRecent.Contains(m))
            .ToList();

        if (!summarizable.Any())
        {
            return;
        }

        List<ChatMessage> summaryChunks = await SummarizeWithLlmAsync(summarizable, allowedSummaryTokens, cancellationToken)
            ?? BuildSummaryChunks(summarizable, maxTokens, allowedSummaryTokens);

        List<ChatMessage> rebuilt = new List<ChatMessage>();
        rebuilt.AddRange(systemMessages);
        rebuilt.AddRange(summaryChunks);
        rebuilt.AddRange(keptRecent.Where(m => !IsToolPayload(m)));

        Conversation.Clear();
        Conversation.AddMessage(rebuilt);
    }

    private async Task<List<ChatMessage>?> SummarizeWithLlmAsync(
        List<ChatMessage> messagesToSummarize,
        int allowedSummaryTokens,
        CancellationToken cancellationToken)
    {
        string context = BuildSummarizationContext(messagesToSummarize, allowedSummaryTokens);

        ChatRequestResponseFormats responseFormat = BuildSummaryResponseFormat();

        TornadoAgent summarizerAgent = new TornadoAgent(
            _client,
            _model,
            name: "Summarizer",
            instructions: "Summarize the dialog into ordered assistant summary chunks.",
            options: new ChatRequest
            {
                Model = _model,
                Tools = [],
                ResponseFormat = responseFormat
            });

        string prompt =
            $"Summarize the following history. Target total size: ~{allowedSummaryTokens} tokens. " +
            "Strip tool payloads but mention tool outcomes. Return only the structured JSON per schema.\n" +
            context;

        Conversation summaryConv = await summarizerAgent.Run(prompt, cancellationToken: cancellationToken);
        string? response = summaryConv.Messages.LastOrDefault()?.Content;

        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        List<string> chunks = ParseSummaries(response);

        if (chunks.Count == 0)
        {
            return null;
        }

        return chunks
            .Select(text => new ChatMessage(ChatMessageRoles.Assistant, text))
            .ToList();
    }

    private string BuildSummarizationContext(List<ChatMessage> messages, int allowedSummaryTokens)
    {
        int charBudget = Math.Max(800, allowedSummaryTokens * 4);
        StringBuilder sb = new StringBuilder();
        foreach (ChatMessage msg in messages)
        {
            if (sb.Length >= charBudget)
            {
                break;
            }

            string role = msg.Role?.ToString() ?? "Unknown";
            string content = SafeContent(msg);

            if (IsToolPayload(msg))
            {
                string toolName = msg.ToolCallId ?? msg.Name ?? "tool";
                string status = msg.ToolInvocationSucceeded.HasValue
                    ? (msg.ToolInvocationSucceeded.Value ? "succeeded" : "failed")
                    : "completed";
                content = $"[TOOL] {toolName} {status}: {content}";
            }

            sb.AppendLine($"{role}: {content}");
        }

        if (sb.Length > charBudget)
        {
            return sb.ToString().Substring(sb.Length - charBudget, charBudget);
        }

        return sb.ToString();
    }

    private List<ChatMessage> GetRecentMessages(List<ChatMessage> nonSystem, int maxTokens)
    {
        List<ChatMessage> recent = new List<ChatMessage>();
        int exchangesKept = 0;
        double largeThreshold = maxTokens * LargeKeepFraction;

        for (int i = nonSystem.Count - 1; i >= 0 && exchangesKept < 3; i--)
        {
            ChatMessage candidate = nonSystem[i];

            if (IsToolPayload(candidate))
            {
                continue; // tools are summarized
            }

            if (candidate.GetMessageTokens() > largeThreshold)
            {
                continue; // too large to keep as-is
            }

            if (candidate.Role is ChatMessageRoles.User or ChatMessageRoles.Assistant)
            {
                recent.Insert(0, candidate);

                // Count an exchange when we capture a user message; assistant follows naturally
                if (candidate.Role == ChatMessageRoles.User)
                {
                    exchangesKept++;
                }
            }
        }

        return recent;
    }

    private List<ChatMessage> BuildSummaryChunks(List<ChatMessage> messagesToSummarize, int maxTokens, int allowedSummaryTokens)
    {
        List<ChatMessage> summaries = new List<ChatMessage>();
        if (!messagesToSummarize.Any())
        {
            return summaries;
        }

        int chunkTokenCap = Math.Max(MinChunkTokens, allowedSummaryTokens / Math.Max(1, (messagesToSummarize.Count / 4) + 1));
        int totalSummaryTokens = 0;
        int chunkIndex = 1;
        List<string> chunkLines = new List<string>();
        int chunkTokens = 0;

        foreach (ChatMessage msg in messagesToSummarize)
        {
            string line = FormatSummaryLine(msg);
            int lineTokens = Math.Max(1, line.Length / 4);

            if (chunkTokens + lineTokens > chunkTokenCap && chunkLines.Count > 0)
            {
                summaries.Add(CreateSummaryMessage(chunkIndex++, chunkLines));
                totalSummaryTokens += chunkTokens;
                chunkLines = new List<string>();
                chunkTokens = 0;

                if (totalSummaryTokens >= allowedSummaryTokens)
                {
                    break; // stop if we've hit our target budget
                }
            }

            if (totalSummaryTokens + chunkTokens + lineTokens > allowedSummaryTokens)
            {
                // Hard stop to avoid exceeding budget
                break;
            }

            chunkLines.Add(line);
            chunkTokens += lineTokens;
        }

        if (chunkLines.Count > 0 && totalSummaryTokens < allowedSummaryTokens)
        {
            summaries.Add(CreateSummaryMessage(chunkIndex, chunkLines));
        }

        return summaries;
    }

    private static ChatMessage CreateSummaryMessage(int chunkIndex, List<string> lines)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Summary chunk {chunkIndex} (older context):");
        sb.AppendLine();
        sb.Append(string.Join(Environment.NewLine, lines));
        return new ChatMessage(ChatMessageRoles.Assistant, sb.ToString());
    }

    private string FormatSummaryLine(ChatMessage message)
    {
        string roleLabel = message.Role?.ToString() ?? "Unknown";

        if (message.Role == ChatMessageRoles.Tool)
        {
            string toolName = message.ToolCallId ?? message.Name ?? "tool";
            string toolStatus = message.ToolInvocationSucceeded.HasValue
                ? (message.ToolInvocationSucceeded.Value ? "succeeded" : "failed")
                : "completed";
            string content = Truncate(SafeContent(message), 220);
            return $"{roleLabel} {toolName}: {toolStatus}; result: {content}";
        }

        if (message.ToolCalls?.Count > 0)
        {
            string toolNames = string.Join(", ", message.ToolCalls.Select(c => c.FunctionCall?.Name ?? c.CustomCall?.Name ?? "tool"));
            return $"{roleLabel}: requested tools [{toolNames}]; awaiting or handled separately.";
        }

        string summaryContent = Truncate(SafeContent(message), 260);
        return $"{roleLabel}: {summaryContent}";
    }

    private static string SafeContent(ChatMessage message)
    {
        string content = message.GetMessageContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            return "(no content)";
        }

        return content.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string Truncate(string value, int maxChars)
    {
        if (value.Length <= maxChars)
        {
            return value;
        }

        return value.Substring(0, maxChars) + "...";
    }

    private static bool IsToolPayload(ChatMessage message)
    {
        return message.Role == ChatMessageRoles.Tool
               || (message.ToolCalls?.Count > 0)
               || !string.IsNullOrWhiteSpace(message.ToolCallId);
    }

    private int GetMaxContextTokens()
    {
        return Conversation?.RequestParameters?.Model?.ContextTokens ?? FallbackMaxTokens;
    }
}

