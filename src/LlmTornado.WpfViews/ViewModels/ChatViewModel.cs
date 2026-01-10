using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;
using Microsoft.Win32;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the chat conversation panel.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    private readonly ConversationStore _conversationStore;
    private readonly Stopwatch _requestStopwatch = new();
    private MessageViewModel? _currentStreamingMessage;
    
    [ObservableProperty]
    private ConversationModel? _currentConversation;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = string.Empty;
    
    [ObservableProperty]
    private bool _isProcessing;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private bool _isInitialized;
    
    [ObservableProperty]
    private string _statusText = "Ready";
    
    [ObservableProperty]
    private long _responseTimeMs;
    
    [ObservableProperty]
    private int _totalTokens;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    [ObservableProperty]
    private string _systemPromptContent = string.Empty;
    
    [ObservableProperty]
    private ToolCallRequest? _pendingToolCall;
    
    [ObservableProperty]
    private bool _isToolApprovalVisible;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingAttachments))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearAttachmentsCommand))]
    private int _pendingAttachmentsCount;
    
    /// <summary>
    /// Collection of messages in the current conversation.
    /// </summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    
    /// <summary>
    /// History of tool calls in this session.
    /// </summary>
    public ObservableCollection<ToolCallRequest> ToolCallHistory { get; } = [];
    
    /// <summary>
    /// Collection of pending file attachments to send with the next message.
    /// </summary>
    public ObservableCollection<FileAttachmentViewModel> PendingAttachments { get; } = [];
    
    /// <summary>
    /// Whether there are pending attachments.
    /// </summary>
    public bool HasPendingAttachments => PendingAttachmentsCount > 0;
    
    /// <summary>
    /// Event raised when scrolling to bottom is requested.
    /// </summary>
    public event Action? ScrollToBottomRequested;
    
    /// <summary>
    /// Event raised when conversation is saved (for refreshing the list).
    /// </summary>
    public event Action? ConversationSaved;
    
    /// <summary>
    /// Event raised when a tool approval is requested.
    /// </summary>
    public event Action<ToolCallRequest>? ToolApprovalRequested;
    
    public ChatViewModel(ChatService chatService, ConversationStore conversationStore)
    {
        _chatService = chatService;
        _conversationStore = conversationStore;
        
        // Wire up service events
        _chatService.OnStreamingDelta += OnStreamingDelta;
        _chatService.OnProcessingStarted += OnProcessingStarted;
        _chatService.OnProcessingCompleted += OnProcessingCompleted;
        _chatService.OnProcessingError += OnProcessingError;
        _chatService.OnProcessingCancelled += OnProcessingCancelled;
        _chatService.OnUsageReceived += OnUsageReceived;
        _chatService.OnToolApprovalRequired += OnToolApprovalRequired;
        _chatService.OnToolCompleted += OnToolCompleted;
        
        // Track pending attachments count for property change notifications
        PendingAttachments.CollectionChanged += (_, _) => PendingAttachmentsCount = PendingAttachments.Count;
    }
    
    /// <summary>
    /// Initializes the chat service with settings.
    /// </summary>
    public async Task InitializeAsync(SettingsModel settings, string? systemPrompt = null)
    {
        try
        {
            await _chatService.InitializeAsync(settings, systemPrompt);
            SystemPromptContent = systemPrompt ?? string.Empty;
            IsInitialized = true;
            StatusText = "Connected";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Connection failed";
            IsInitialized = false;
        }
    }
    
    /// <summary>
    /// Loads a conversation by ID.
    /// </summary>
    public async Task LoadConversationAsync(string conversationId)
    {
        var conversation = await _conversationStore.LoadConversationAsync(conversationId);
        if (conversation != null)
        {
            CurrentConversation = conversation;
            Messages.Clear();
            
            foreach (var message in conversation.Messages)
            {
                Messages.Add(MessageViewModel.FromModel(message));
            }
            
            ScrollToBottomRequested?.Invoke();
        }
    }
    
    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    [RelayCommand]
    public void NewConversation()
    {
        CurrentConversation = new ConversationModel();
        Messages.Clear();
        _chatService.ClearConversation();
        StatusText = "New conversation";
    }
    
    /// <summary>
    /// Sends the current input message.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    public async Task SendMessageAsync()
    {
        var hasText = !string.IsNullOrWhiteSpace(InputText);
        var hasAttachments = PendingAttachments.Count > 0;
        
        if ((!hasText && !hasAttachments) || !IsInitialized)
            return;
        
        var userMessageText = InputText.Trim();
        InputText = string.Empty;
        
        // Capture current attachments and clear the pending list
        var attachments = PendingAttachments.Select(a => a.Model).ToList();
        PendingAttachments.Clear();
        
        // Add user message to UI with attachments
        var userMessage = new MessageViewModel
        {
            Role = MessageRole.User,
            Content = userMessageText
        };
        
        // Add attachment info to the message
        foreach (var attachment in attachments)
        {
            userMessage.AddAttachment(new FileAttachmentModel
            {
                Id = attachment.Id,
                FilePath = attachment.FilePath,
                FileName = attachment.FileName,
                FileType = attachment.FileType,
                FileSize = attachment.FileSize,
                MimeType = attachment.MimeType,
                Base64Content = attachment.Base64Content
            });
        }
        
        Messages.Add(userMessage);
        
        // Update conversation title if this is the first message
        if (CurrentConversation != null && Messages.Count == 1)
        {
            var title = hasText ? userMessageText : $"[{attachments.Count} attachment(s)]";
            CurrentConversation.Title = title.Length > 50 
                ? title[..50] + "..." 
                : title;
        }
        
        ScrollToBottomRequested?.Invoke();
        
        try
        {
            _requestStopwatch.Restart();
            await _chatService.SendMessageAsync(userMessageText, attachments);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _requestStopwatch.Stop();
            await SaveCurrentConversationAsync();
        }
    }
    
    private bool CanSendMessage()
    {
        return !IsProcessing && IsInitialized && (!string.IsNullOrWhiteSpace(InputText) || HasPendingAttachments);
    }
    
    /// <summary>
    /// Cancels the current request.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _chatService.CancelRequest();
    }
    
    private bool CanCancel() => IsProcessing;
    
    /// <summary>
    /// Saves the current conversation to disk.
    /// </summary>
    private async Task SaveCurrentConversationAsync()
    {
        if (CurrentConversation == null)
            return;
        
        CurrentConversation.Messages = Messages.Select(m => m.ToModel()).ToList();
        await _conversationStore.SaveConversationAsync(CurrentConversation);
        
        // Notify that conversation was saved so the list can be refreshed
        ConversationSaved?.Invoke();
    }
    
    #region Service Event Handlers
    
    private void OnStreamingDelta(string delta)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _currentStreamingMessage?.AppendContent(delta);
            ScrollToBottomRequested?.Invoke();
        });
    }
    
    private void OnProcessingStarted()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = true;
            StatusText = "Processing...";
            ErrorMessage = null;
            
            // Create streaming message placeholder
            _currentStreamingMessage = new MessageViewModel
            {
                Role = MessageRole.Assistant
            };
            _currentStreamingMessage.StartStreaming();
            Messages.Add(_currentStreamingMessage);
            
            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }
    
    private void OnProcessingCompleted(LlmTornado.Chat.ChatMessage response)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            ResponseTimeMs = _requestStopwatch.ElapsedMilliseconds;
            StatusText = $"Completed in {ResponseTimeMs}ms";
            
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();
                
                // If no content was streamed, set the full response
                if (string.IsNullOrEmpty(_currentStreamingMessage.Content))
                {
                    _currentStreamingMessage.Content = response.Content ?? string.Empty;
                }
            }
            
            _currentStreamingMessage = null;
            
            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ScrollToBottomRequested?.Invoke();
        });
    }
    
    private void OnProcessingError(Exception ex)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            ErrorMessage = ex.Message;
            StatusText = "Error";
            
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();
                _currentStreamingMessage.Content += $"\n\n[Error: {ex.Message}]";
            }
            
            _currentStreamingMessage = null;
            
            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }
    
    private void OnProcessingCancelled()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            StatusText = "Cancelled";
            
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.EndStreaming();
                _currentStreamingMessage.Content += "\n\n[Cancelled by user]";
            }
            
            _currentStreamingMessage = null;
            
            SendMessageCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        });
    }
    
    private void OnUsageReceived(int inputTokens, int outputTokens, int totalTokens)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TotalTokens = totalTokens;
            
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.TokenCount = outputTokens;
            }
        });
    }
    
    private void OnToolApprovalRequired(ToolCallRequest request)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Add to history
            ToolCallHistory.Insert(0, request);
            
            // Set as pending for display
            PendingToolCall = request;
            IsToolApprovalVisible = true;
            
            // Update status
            StatusText = $"⚠️ Tool requesting approval: {request.ToolName}";
            
            // Raise event for the main view model to show dialog
            ToolApprovalRequested?.Invoke(request);
        });
    }
    
    private void OnToolCompleted(string toolName, string result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Update the request in history
            var request = ToolCallHistory.FirstOrDefault(r => r.ToolName == toolName && r.Status == ToolApprovalStatus.Approved);
            if (request != null)
            {
                request.Status = ToolApprovalStatus.Completed;
                request.Result = result;
            }
            
            // Update streaming message with result preview
            if (_currentStreamingMessage != null)
            {
                var preview = result.Length > 200 ? result[..200] + "..." : result;
                _currentStreamingMessage.AppendContent($"\n🔧 **Tool Result ({toolName}):**\n```\n{preview}\n```\n\n");
            }
            
            IsToolApprovalVisible = false;
            PendingToolCall = null;
            StatusText = $"Tool completed: {toolName}";
        });
    }
    
    /// <summary>
    /// Approves the pending tool call.
    /// </summary>
    [RelayCommand]
    public void ApproveToolCall()
    {
        if (PendingToolCall?.ApprovalTask != null)
        {
            // Add indicator to streaming message
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.AppendContent($"\n\n✅ **Tool Approved: {PendingToolCall.ToolName}**\n");
            }
            
            StatusText = $"Executing tool: {PendingToolCall.ToolName}";
            IsToolApprovalVisible = false;
            
            // Complete the task to allow execution
            PendingToolCall.ApprovalTask.TrySetResult(true);
        }
    }
    
    /// <summary>
    /// Denies the pending tool call.
    /// </summary>
    [RelayCommand]
    public void DenyToolCall()
    {
        if (PendingToolCall?.ApprovalTask != null)
        {
            // Add indicator to streaming message
            if (_currentStreamingMessage != null)
            {
                _currentStreamingMessage.AppendContent($"\n\n❌ **Tool Denied: {PendingToolCall.ToolName}**\n");
            }
            
            StatusText = $"Tool denied: {PendingToolCall.ToolName}";
            PendingToolCall.Status = ToolApprovalStatus.Denied;
            IsToolApprovalVisible = false;
            
            // Complete the task with false to deny execution
            PendingToolCall.ApprovalTask.TrySetResult(false);
            PendingToolCall = null;
        }
    }
    
    /// <summary>
    /// Clears the tool call history.
    /// </summary>
    [RelayCommand]
    public void ClearToolHistory()
    {
        ToolCallHistory.Clear();
    }
    
    #endregion
    
    #region Attachment Commands
    
    /// <summary>
    /// Opens a file dialog to add attachments.
    /// </summary>
    [RelayCommand]
    public void AddAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Filter = FileAttachmentModel.FileDialogFilter,
            Multiselect = true,
            Title = "Select files to attach"
        };
        
        if (dialog.ShowDialog() == true)
        {
            AddFilesFromPaths(dialog.FileNames);
        }
    }
    
    /// <summary>
    /// Removes a specific attachment from the pending list.
    /// </summary>
    [RelayCommand]
    public void RemoveAttachment(FileAttachmentViewModel? attachment)
    {
        if (attachment != null)
        {
            PendingAttachments.Remove(attachment);
        }
    }
    
    /// <summary>
    /// Clears all pending attachments.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasPendingAttachments))]
    public void ClearAttachments()
    {
        PendingAttachments.Clear();
    }
    
    /// <summary>
    /// Adds files from drag-and-drop or paste operations.
    /// </summary>
    public void AddFilesFromPaths(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            try
            {
                var (isValid, error) = FileAttachmentViewModel.ValidateFile(filePath);
                if (!isValid)
                {
                    ErrorMessage = $"Cannot add {Path.GetFileName(filePath)}: {error}";
                    continue;
                }
                
                var attachment = new FileAttachmentViewModel(filePath);
                attachment.RemoveRequested += OnAttachmentRemoveRequested;
                PendingAttachments.Add(attachment);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add {Path.GetFileName(filePath)}: {ex.Message}";
            }
        }
        
        // Clear error after a delay if files were added successfully
        if (PendingAttachments.Count > 0 && ErrorMessage != null)
        {
            _ = ClearErrorAfterDelayAsync();
        }
    }
    
    /// <summary>
    /// Handles files dropped onto the chat area.
    /// </summary>
    public void HandleFileDrop(string[] files)
    {
        var supportedFiles = files.Where(f => 
            FileAttachmentModel.IsSupportedExtension(Path.GetExtension(f)));
        
        AddFilesFromPaths(supportedFiles);
    }
    
    private void OnAttachmentRemoveRequested(FileAttachmentViewModel attachment)
    {
        attachment.RemoveRequested -= OnAttachmentRemoveRequested;
        PendingAttachments.Remove(attachment);
    }
    
    private async Task ClearErrorAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (ErrorMessage?.Contains("Cannot add") == true || ErrorMessage?.Contains("Failed to add") == true)
        {
            ErrorMessage = null;
        }
    }
    
    #endregion
}

