using System.Windows;
using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service that bridges the WPF UI with LLMTornado ChatRuntime.
/// Handles message sending, streaming, MCP tools, and cancellation.
/// </summary>
public class ChatService
{
    private ChatRuntime? _runtime;
    private TornadoAgent? _agent;
    private TornadoApi? _api;
    private bool _isProcessing;
    private DateTime _requestStartTime;
    private ToolCallRequest? _pendingToolRequest;
    private McpServerManager? _mcpServerManager;
    
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
    /// Sets the MCP server manager for loading MCP tools.
    /// </summary>
    public void SetMcpServerManager(McpServerManager manager)
    {
        _mcpServerManager = manager;
    }

    /// <summary>
    /// Initializes the chat service with the given settings.
    /// </summary>
    public async Task InitializeAsync(SettingsModel settings, string? systemPrompt = null, ModelOption? selectedModelOption = null)
    {
        var authList = new List<ProviderAuthentication>();
        TornadoApi? customApi = null;
        
        foreach (var kvp in settings.ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                if (kvp.Key == LLmProviders.AzureOpenAi)
                {
                    if (!string.IsNullOrWhiteSpace(settings.AzureEndpoint))
                    {
                        authList.Add(new ProviderAuthentication(kvp.Key, kvp.Value, settings.AzureOrganization));
                    }
                }
                else if (kvp.Key != LLmProviders.Custom)
                {
                    authList.Add(new ProviderAuthentication(kvp.Key, kvp.Value));
                }
            }
        }
        
        if (settings.CustomEndpoints.TryGetValue(LLmProviders.Custom, out var customEndpoint) && 
            !string.IsNullOrWhiteSpace(customEndpoint))
        {
            if (Uri.TryCreate(customEndpoint, UriKind.Absolute, out var customUri))
            {
                var customApiKey = settings.ApiKeys.TryGetValue(LLmProviders.Custom, out var key) && !string.IsNullOrWhiteSpace(key) 
                    ? key 
                    : string.Empty;
                
                customApi = new TornadoApi(customUri, customApiKey, LLmProviders.Custom);
            }
        }
        
        if (authList.Count > 0)
        {
            _api = new TornadoApi(authList);
            
            if (customApi != null)
            {
                _api.ApiUrlFormat = customApi.ApiUrlFormat;
            }
        }
        else if (customApi != null)
        {
            _api = customApi;
        }
        else
        {
            throw new InvalidOperationException("At least one provider must be configured (API key or custom endpoint).");
        }
        
        LLmProviders modelProvider = LLmProviders.OpenAi;
        string modelName = settings.SelectedModelId;
        
        if (selectedModelOption != null)
        {
            modelProvider = selectedModelOption.ProviderEnum;
            modelName = selectedModelOption.ApiName ?? selectedModelOption.Id;
        }
        else
        {
            var inferredProvider = ChatModel.GetProvider(modelName);
            if (inferredProvider.HasValue)
            {
                modelProvider = inferredProvider.Value;
            }
        }
        
        var chatOptions = new ChatRequest
        {
            Model = modelName,
            MaxTokens = settings.MaxTokens,
            Temperature = settings.Temperature
        };
        
        var model = new ChatModel(modelName, modelProvider);
        var toolPermissionRequired = new Dictionary<string, bool>();
        var allTools = new List<Delegate>();
        
        // Load MCP tools if manager is available
        if (_mcpServerManager != null)
        {
            // Initialize all MCP servers
            await _mcpServerManager.InitializeAllServersAsync();
        }
        
        // Create the agent
        _agent = new TornadoAgent(
            _api, 
            model,
            instructions: systemPrompt ?? "You are a helpful assistant",
            streaming: settings.EnableStreaming,
            options: chatOptions,
            tools: allTools,
            toolPermissionRequired: toolPermissionRequired
        );
        
        // Add MCP tools to the agent after creation
        if (_mcpServerManager != null)
        {
            var mcpTools = _mcpServerManager.GetAllTools();
            foreach (var tool in mcpTools)
            {
                _agent.AddTool(tool);
                // Set default permission (require approval by default)
                var toolName = tool.Function?.Name;
                if (!string.IsNullOrEmpty(toolName) && !toolPermissionRequired.ContainsKey(toolName))
                {
                    toolPermissionRequired[toolName] = true;
                }
            }
            // Update the agent's tool permission dictionary
            _agent.ToolPermissionRequired = toolPermissionRequired;
        }
        
        var config = new SingletonRuntimeConfiguration(_agent)
        {
            OnRuntimeEvent = HandleRuntimeEventAsync,
            OnRuntimeRequestEvent = HandleToolPermissionRequestAsync
        };
        
        _runtime = new ChatRuntime(config);
        
        await Task.CompletedTask;
    }
    
    private async ValueTask<bool> HandleToolPermissionRequestAsync(string requestMessage)
    {
        var (toolName, arguments) = ParseToolPermissionRequest(requestMessage);
        
        var request = new ToolCallRequest
        {
            ToolName = toolName,
            Arguments = arguments,
            Status = ToolApprovalStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            ApprovalTask = new TaskCompletionSource<bool>()
        };
        
        _pendingToolRequest = request;
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            OnToolApprovalRequired?.Invoke(request);
        });
        
        var approved = await request.ApprovalTask.Task;
        
        request.Status = approved ? ToolApprovalStatus.Approved : ToolApprovalStatus.Denied;
        _pendingToolRequest = null;
        
        return approved;
    }
    
    private static (string ToolName, string Arguments) ParseToolPermissionRequest(string message)
    {
        var toolName = "Unknown";
        var arguments = "{}";
        
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

    /// <summary>
    /// Sends a message and returns the response.
    /// </summary>
    public async Task<ChatMessage?> SendMessageAsync(string content, CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync(content, null, cancellationToken);
    }
    
    /// <summary>
    /// Sends a message with optional file attachments and returns the response.
    /// </summary>
    public async Task<ChatMessage?> SendMessageAsync(string content, List<FileAttachmentModel>? attachments, CancellationToken cancellationToken = default)
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
            
            ChatMessage userMessage;
            
            if (attachments != null && attachments.Count > 0)
            {
                var parts = new List<ChatMessagePart>();
                
                if (!string.IsNullOrWhiteSpace(content))
                {
                    parts.Add(new ChatMessagePart(content));
                }
                
                foreach (var attachment in attachments)
                {
                    var part = ConvertAttachmentToPart(attachment);
                    if (part != null)
                    {
                        parts.Add(part);
                    }
                }
                
                userMessage = new ChatMessage(ChatMessageRoles.User, parts);
            }
            else
            {
                userMessage = new ChatMessage(ChatMessageRoles.User, content);
            }
            
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
    
    private static ChatMessagePart? ConvertAttachmentToPart(FileAttachmentModel attachment)
    {
        if (string.IsNullOrEmpty(attachment.Base64Content))
        {
            return null;
        }
        
        switch (attachment.FileType)
        {
            case FileAttachmentType.Image:
                var imageDataUrl = $"data:{attachment.MimeType};base64,{attachment.Base64Content}";
                var chatImage = new ChatImage(imageDataUrl);
                return new ChatMessagePart(chatImage);
                
            case FileAttachmentType.Document:
                var chatDocument = new ChatDocument(attachment.Base64Content);
                return new ChatMessagePart(chatDocument);
                
            default:
                return null;
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
    public async Task UpdateSystemPromptAsync(SettingsModel settings, string systemPrompt, ModelOption? selectedModelOption = null)
    {
        ClearConversation();
        await InitializeAsync(settings, systemPrompt, selectedModelOption);
    }
    
    private async ValueTask HandleRuntimeEventAsync(ChatRuntimeEvents evt)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            switch (evt)
            {
                case ChatRuntimeStartedEvent:
                    break;
                    
                case ChatRuntimeCompletedEvent:
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
    
    private void HandleToolInvoked(AgentRunnerToolInvokedEvent toolEvent)
    {
        var toolName = toolEvent.ToolCalled.Name ?? "Unknown";
        OnToolInvoked?.Invoke(toolName);
    }
    
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
