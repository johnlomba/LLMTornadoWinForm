using System.Windows;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.WpfViews.Models;

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
    /// Raised when tool is invoked.
    /// </summary>
    public event Action<string>? OnToolInvoked;
    
    /// <summary>
    /// Raised when tool completes.
    /// </summary>
    public event Action<string, string>? OnToolCompleted;
    
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
            options: chatOptions
        );
        
        // Create runtime configuration
        var config = new SingletonRuntimeConfiguration(_agent)
        {
            OnRuntimeEvent = HandleRuntimeEventAsync
        };
        
        // Create the runtime
        _runtime = new ChatRuntime(config);
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
                OnToolInvoked?.Invoke(toolInvoked.ToolCalled.Name ?? "Unknown");
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

