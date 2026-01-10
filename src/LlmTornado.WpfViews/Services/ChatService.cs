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
        // Build list of provider authentications from settings
        var authList = new List<ProviderAuthentication>();
        TornadoApi? customApi = null;
        
        foreach (var kvp in settings.ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                // Handle Azure specially
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
        
        // Handle Custom provider separately (requires endpoint URL)
        if (settings.CustomEndpoints.TryGetValue(LLmProviders.Custom, out var customEndpoint) && 
            !string.IsNullOrWhiteSpace(customEndpoint))
        {
            if (Uri.TryCreate(customEndpoint, UriKind.Absolute, out var customUri))
            {
                // Create a separate TornadoApi for custom provider with endpoint
                var customApiKey = settings.ApiKeys.TryGetValue(LLmProviders.Custom, out var key) && !string.IsNullOrWhiteSpace(key) 
                    ? key 
                    : string.Empty;
                
                customApi = new TornadoApi(customUri, customApiKey, LLmProviders.Custom);
            }
        }
        
        // Create the main TornadoApi instance with multiple providers (like demo's ConnectMulti)
        if (authList.Count > 0)
        {
            _api = new TornadoApi(authList);
            
            // If we have a custom API, merge it into the main API
            if (customApi != null)
            {
                // Copy custom endpoint configuration
                _api.ApiUrlFormat = customApi.ApiUrlFormat;
                
                // Add custom authentication if present
                if (customApi.Authentications.TryGetValue(LLmProviders.Custom, out var customAuth))
                {
                    _api.Authentications.TryAdd(LLmProviders.Custom, customAuth);
                }
            }
        }
        else if (customApi != null)
        {
            // Only custom provider configured
            _api = customApi;
        }
        else
        {
            throw new InvalidOperationException("At least one provider must be configured (API key or custom endpoint).");
        }
        
        // Determine the provider and model name for the selected model
        LLmProviders modelProvider = LLmProviders.OpenAi;
        string modelName = settings.SelectedModelId;
        
        if (selectedModelOption != null)
        {
            modelProvider = selectedModelOption.ProviderEnum;
            modelName = selectedModelOption.ApiName ?? selectedModelOption.Id;
        }
        else
        {
            // Try to infer provider from model name
            var inferredProvider = ChatModel.GetProvider(modelName);
            if (inferredProvider.HasValue)
            {
                modelProvider = inferredProvider.Value;
            }
        }
        
        // Create chat options
        var chatOptions = new ChatRequest
        {
            Model = modelName,
            MaxTokens = settings.MaxTokens,
            Temperature = settings.Temperature
        };
        
        // Create the model - use ChatModel constructor with provider
        var model = new ChatModel(modelName, modelProvider);
        
        // Build tool permission dictionary
        var toolPermissionRequired = new Dictionary<string, bool>
        {
            { "GetCurrentWeather", true }
        };

        // Load MCP tools if manager is available
        var allTools = new List<Delegate>();
        if (_mcpServerManager != null)
        {
            // Initialize all MCP servers
            await _mcpServerManager.InitializeAllServersAsync();
            
            // Get all MCP tools
            var mcpTools = _mcpServerManager.GetAllTools();
            
            // Add MCP tools to the agent (we'll add them after agent creation)
            // For now, we'll add them as delegates are expected, but MCP tools are Tool objects
            // We need to add them after agent creation using AddTool
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
                // Set default permission (can be configured per tool)
                if (tool.Function.Name != null && !toolPermissionRequired.ContainsKey(tool.Function.Name))
                {
                    toolPermissionRequired[tool.Function.Name] = true; // Require approval by default
                }
            }
            // Update the agent's tool permission dictionary
            _agent.ToolPermissionRequired = toolPermissionRequired;
        }
        
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
            
            // Create the user message
            ChatMessage userMessage;
            
            if (attachments != null && attachments.Count > 0)
            {
                // Build message parts for multi-part message
                var parts = new List<ChatMessagePart>();
                
                // Add text content if present
                if (!string.IsNullOrWhiteSpace(content))
                {
                    parts.Add(new ChatMessagePart(content));
                }
                
                // Add file attachments as parts
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
                // Simple text message
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
    
    /// <summary>
    /// Converts a FileAttachmentModel to a ChatMessagePart.
    /// </summary>
    private static ChatMessagePart? ConvertAttachmentToPart(FileAttachmentModel attachment)
    {
        if (string.IsNullOrEmpty(attachment.Base64Content))
        {
            return null;
        }
        
        switch (attachment.FileType)
        {
            case FileAttachmentType.Image:
                // Create data URL for image
                var imageDataUrl = $"data:{attachment.MimeType};base64,{attachment.Base64Content}";
                var chatImage = new ChatImage(imageDataUrl);
                return new ChatMessagePart(chatImage);
                
            case FileAttachmentType.Document:
                // Create ChatDocument for PDF (Anthropic only)
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

