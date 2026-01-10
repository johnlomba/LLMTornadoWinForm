using System.Windows;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.WpfViews.Models;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LlmTornado.WpfViews.Services;

/// <summary>
/// Service that bridges the WPF UI with LLMTornado ChatRuntime.
/// Handles message sending, streaming, and cancellation.
/// </summary>
public class ChatService
{
    private ChatRuntime? _runtime;
    private TornadoAgent? _agent;
    private TornadoApi? _api;
    private bool _isProcessing;
    private DateTime _requestStartTime;
    private ToolCallRequest? _pendingToolRequest;
    
    /// <summary>
    /// Whether tool calls require user approval before execution.
    /// </summary>
    public bool RequireToolApproval { get; set; } = true;
    
    /// <summary>
    /// Raised when a streaming text delta is received.
    /// </summary>
    public event Action<string>? OnStreamingDelta;
    
    /// <summary>
    /// Raised when processing starts.
    /// </summary>
    public event Action? OnProcessingStarted;
    
    /// <summary>
    /// Raised when processing completes successfully.
    /// </summary>
    public event Action<ChatMessage>? OnProcessingCompleted;
    
    /// <summary>
    /// Raised when processing fails with an error.
    /// </summary>
    public event Action<Exception>? OnProcessingError;
    
    /// <summary>
    /// Raised when processing is cancelled.
    /// </summary>
    public event Action? OnProcessingCancelled;
    
    /// <summary>
    /// Raised when usage information is received.
    /// </summary>
    public event Action<int, int, int>? OnUsageReceived;
    
    /// <summary>
    /// Raised when tool is invoked (after execution).
    /// </summary>
    public event Action<string>? OnToolInvoked;
    
    /// <summary>
    /// Raised when tool completes.
    /// </summary>
    public event Action<string, string>? OnToolCompleted;
    
    /// <summary>
    /// Raised when a tool call requires approval BEFORE execution.
    /// The ToolCallRequest.ApprovalTask should be completed with true (approve) or false (deny).
    /// </summary>
    public event Action<ToolCallRequest>? OnToolApprovalRequired;
    
    /// <summary>
    /// Whether the service is currently processing a request.
    /// </summary>
    public bool IsProcessing => _isProcessing;
    
    /// <summary>
    /// Whether the service is initialized and ready.
    /// </summary>
    public bool IsInitialized => _runtime != null;
    
    /// <summary>
    /// Initializes the chat service with the given settings.
    /// </summary>
    public void Initialize(SettingsModel settings, string? systemPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey) && !settings.UseAzure)
        {
            throw new InvalidOperationException("OpenAI API key is required.");
        }
        
        if (settings.UseAzure && (string.IsNullOrWhiteSpace(settings.AzureEndpoint) || string.IsNullOrWhiteSpace(settings.AzureApiKey)))
        {
            throw new InvalidOperationException("Azure endpoint and API key are required when using Azure.");
        }
        
        // Create the TornadoApi instance
        if (settings.UseAzure)
        {
            _api = new TornadoApi(LLmProviders.AzureOpenAi, settings.AzureApiKey!);
        }
        else
        {
            _api = new TornadoApi(LLmProviders.OpenAi, settings.OpenAiApiKey!);
        }
        
        // Create chat options
        var chatOptions = new ChatRequest
        {
            Model = settings.SelectedModelId,
            MaxTokens = settings.MaxTokens,
            Temperature = settings.Temperature
        };
        
        // Get the model based on selection
        var model = settings.SelectedModelId switch
        {
            "gpt-4o-mini" => ChatModel.OpenAi.Gpt4.OMini,
            "gpt-4-turbo" => ChatModel.OpenAi.Gpt4.Turbo,
            "gpt-3.5-turbo" => ChatModel.OpenAi.Gpt35.Turbo,
            "o3-mini" => ChatModel.OpenAi.O3.Mini,
            "o4-mini" => ChatModel.OpenAi.O4.V4Mini,
            _ => ChatModel.OpenAi.Gpt4.O
        };
        
        // Create the agent
        _agent = new TornadoAgent(
            _api, 
            model,
            instructions: systemPrompt ?? "You are a helpful assistant",
            streaming: settings.EnableStreaming,
            options: chatOptions,
            tools: [GetCurrentWeather],
             toolPermissionRequired:new Dictionary<string, bool>()
                {
                    { "GetCurrentWeather", true }
                }
        );
        
        // Create runtime configuration
        var config = new SingletonRuntimeConfiguration(_agent)
        {
            OnRuntimeEvent = HandleRuntimeEventAsync,
            OnRuntimeRequestEvent = HandleToolPermissionRequestAsync
        };
        
        // Create the runtime
        _runtime = new ChatRuntime(config);
    }
    
    /// <summary>
    /// Handles tool permission requests from LLMTornado.
    /// This is called BEFORE the tool executes - returning false will deny execution.
    /// </summary>
    /// <param name="requestMessage">Message in format "Tool: {name}\nArguments: {args}"</param>
    /// <returns>True to approve, false to deny</returns>
    private async ValueTask<bool> HandleToolPermissionRequestAsync(string requestMessage)
    {
        // Parse the request message
        var (toolName, arguments) = ParseToolPermissionRequest(requestMessage);
        
        // Create a tool call request
        var request = new ToolCallRequest
        {
            ToolName = toolName,
            Arguments = arguments,
            Status = ToolApprovalStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            ApprovalTask = new TaskCompletionSource<bool>()
        };
        
        _pendingToolRequest = request;
        
        // Raise the event on the UI thread and wait for approval
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            OnToolApprovalRequired?.Invoke(request);
        });
        
        // Wait for the user to approve or deny
        var approved = await request.ApprovalTask.Task;
        
        // Update status based on user's decision
        request.Status = approved ? ToolApprovalStatus.Approved : ToolApprovalStatus.Denied;
        _pendingToolRequest = null;
        
        return approved;
    }
    
    /// <summary>
    /// Parses the tool permission request message.
    /// </summary>
    private static (string ToolName, string Arguments) ParseToolPermissionRequest(string message)
    {
        var toolName = "Unknown";
        var arguments = "{}";
        
        // Parse "Tool: {name}\nArguments: {args}" format
        var lines = message.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("Tool:", StringComparison.OrdinalIgnoreCase))
            {
                toolName = line.Substring(5).Trim();
            }
            else if (line.StartsWith("Arguments:", StringComparison.OrdinalIgnoreCase))
            {
                arguments = line.Substring(10).Trim();
            }
        }
        
        return (toolName, arguments);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Unit
    {
        Celsius, 
        Fahrenheit
    }

    [Description("Get the current weather in a given location")]
    public static string GetCurrentWeather(
        [Description("The city and state, e.g. Boston, MA")] string location,
        [Description("unit of temperature measurement in C or F")] Unit unit = Unit.Celsius)
    {
        // Call the weather API here.
        return $"31 C";
    }
    /// <summary>
    /// Sends a message and returns the response.
    /// </summary>
    public async Task<ChatMessage?> SendMessageAsync(string content, CancellationToken cancellationToken = default)
    {
        if (_runtime == null)
        {
            throw new InvalidOperationException("Chat service not initialized. Call Initialize first.");
        }
        
        if (_isProcessing)
        {
            throw new InvalidOperationException("A request is already in progress.");
        }
        
        _isProcessing = true;
        _requestStartTime = DateTime.UtcNow;
        
        try
        {
            OnProcessingStarted?.Invoke();
            
            var userMessage = new ChatMessage(ChatMessageRoles.User, content);
            var response = await _runtime.InvokeAsync(userMessage);
            
            OnProcessingCompleted?.Invoke(response);
            return response;
        }
        catch (OperationCanceledException)
        {
            OnProcessingCancelled?.Invoke();
            return null;
        }
        catch (Exception ex)
        {
            OnProcessingError?.Invoke(ex);
            throw;
        }
        finally
        {
            _isProcessing = false;
        }
    }
    
    /// <summary>
    /// Cancels the current request.
    /// </summary>
    public void CancelRequest()
    {
        _runtime?.CancelExecution();
    }
    
    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void ClearConversation()
    {
        _runtime?.Clear();
    }
    
    /// <summary>
    /// Gets the current conversation messages.
    /// </summary>
    public List<ChatMessage> GetMessages()
    {
        return _runtime?.RuntimeConfiguration.GetMessages() ?? [];
    }
    
    /// <summary>
    /// Gets the elapsed time since the request started.
    /// </summary>
    public TimeSpan GetElapsedTime()
    {
        return DateTime.UtcNow - _requestStartTime;
    }
    
    /// <summary>
    /// Reinitializes with a new system prompt.
    /// </summary>
    public void UpdateSystemPrompt(SettingsModel settings, string systemPrompt)
    {
        ClearConversation();
        Initialize(settings, systemPrompt);
    }
    
    /// <summary>
    /// Handles runtime events from ChatRuntime.
    /// </summary>
    private async ValueTask HandleRuntimeEventAsync(ChatRuntimeEvents evt)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            switch (evt)
            {
                case ChatRuntimeStartedEvent:
                    // Processing started - already handled
                    break;
                    
                case ChatRuntimeCompletedEvent:
                    // Processing completed - already handled
                    break;
                    
                case ChatRuntimeCancelledEvent:
                    OnProcessingCancelled?.Invoke();
                    break;
                    
                case ChatRuntimeErrorEvent errorEvent:
                    OnProcessingError?.Invoke(errorEvent.Exception);
                    break;
                    
                case ChatRuntimeAgentRunnerEvents runnerEvents:
                    HandleAgentRunnerEvent(runnerEvents.AgentRunnerEvent);
                    break;
            }
        });
    }
    
    /// <summary>
    /// Handles agent runner events (streaming, tool calls, etc.)
    /// </summary>
    private void HandleAgentRunnerEvent(AgentRunnerEvents evt)
    {
        switch (evt)
        {
            case AgentRunnerStreamingEvent streamingEvent:
                HandleStreamingEvent(streamingEvent.ModelStreamingEvent);
                break;
                
            case AgentRunnerToolInvokedEvent toolInvoked:
                HandleToolInvoked(toolInvoked);
                break;
                
            case AgentRunnerToolCompletedEvent toolCompleted:
                OnToolCompleted?.Invoke(
                    toolCompleted.ToolCall.Name ?? "Unknown",
                    toolCompleted.ToolResult?.Content ?? string.Empty);
                break;
                
            case AgentRunnerUsageReceivedEvent usageEvent:
                OnUsageReceived?.Invoke(
                    usageEvent.InputTokens,
                    usageEvent.OutputTokens,
                    usageEvent.TokenUsageAmount);
                break;
                
            case AgentRunnerErrorEvent errorEvent:
                OnProcessingError?.Invoke(errorEvent.Exception ?? new Exception(errorEvent.ErrorMessage));
                break;
        }
    }
    
    /// <summary>
    /// Handles tool invocation events - fires after permission is granted and tool is executing.
    /// </summary>
    private void HandleToolInvoked(AgentRunnerToolInvokedEvent toolEvent)
    {
        var toolName = toolEvent.ToolCalled.Name ?? "Unknown";
        
        // Tool is now executing (permission was already granted via HandleToolPermissionRequestAsync)
        OnToolInvoked?.Invoke(toolName);
    }
    
    /// <summary>
    /// Handles model streaming events.
    /// </summary>
    private void HandleStreamingEvent(ModelStreamingEvents evt)
    {
        switch (evt)
        {
            case ModelStreamingOutputTextDeltaEvent deltaEvent:
                if (!string.IsNullOrEmpty(deltaEvent.DeltaText))
                {
                    OnStreamingDelta?.Invoke(deltaEvent.DeltaText);
                }
                break;
        }
    }
}

