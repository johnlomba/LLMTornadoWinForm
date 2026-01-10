using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

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
    
    /// <summary>
    /// Collection of messages in the current conversation.
    /// </summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    
    /// <summary>
    /// Event raised when scrolling to bottom is requested.
    /// </summary>
    public event Action? ScrollToBottomRequested;
    
    /// <summary>
    /// Event raised when conversation is saved (for refreshing the list).
    /// </summary>
    public event Action? ConversationSaved;
    
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
    }
    
    /// <summary>
    /// Initializes the chat service with settings.
    /// </summary>
    public void Initialize(SettingsModel settings, string? systemPrompt = null)
    {
        try
        {
            _chatService.Initialize(settings, systemPrompt);
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
        if (string.IsNullOrWhiteSpace(InputText) || !IsInitialized)
            return;
        
        var userMessageText = InputText.Trim();
        InputText = string.Empty;
        
        // Add user message to UI
        var userMessage = new MessageViewModel
        {
            Role = MessageRole.User,
            Content = userMessageText
        };
        Messages.Add(userMessage);
        
        // Update conversation title if this is the first message
        if (CurrentConversation != null && Messages.Count == 1)
        {
            CurrentConversation.Title = userMessageText.Length > 50 
                ? userMessageText[..50] + "..." 
                : userMessageText;
        }
        
        ScrollToBottomRequested?.Invoke();
        
        try
        {
            _requestStopwatch.Restart();
            await _chatService.SendMessageAsync(userMessageText);
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
        return !IsProcessing && IsInitialized && !string.IsNullOrWhiteSpace(InputText);
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
    
    #endregion
}

