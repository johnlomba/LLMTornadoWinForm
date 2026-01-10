using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for a single chat message.
/// </summary>
public partial class MessageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private MessageRole _role;
    
    [ObservableProperty]
    private string _content = string.Empty;
    
    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;
    
    [ObservableProperty]
    private bool _isStreaming;
    
    [ObservableProperty]
    private int? _tokenCount;
    
    private readonly StringBuilder _contentBuilder = new();
    
    /// <summary>
    /// Display name for the message sender.
    /// </summary>
    public string RoleDisplayName => Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "Assistant",
        MessageRole.System => "System",
        _ => "Unknown"
    };
    
    /// <summary>
    /// Whether this is a user message.
    /// </summary>
    public bool IsUserMessage => Role == MessageRole.User;
    
    /// <summary>
    /// Whether this is an assistant message.
    /// </summary>
    public bool IsAssistantMessage => Role == MessageRole.Assistant;
    
    /// <summary>
    /// Creates a new message view model from a model.
    /// </summary>
    public static MessageViewModel FromModel(ChatMessageModel model)
    {
        return new MessageViewModel
        {
            Id = model.Id,
            Role = model.Role,
            Content = model.Content,
            Timestamp = model.Timestamp,
            TokenCount = model.TokenCount
        };
    }
    
    /// <summary>
    /// Converts to a chat message model.
    /// </summary>
    public ChatMessageModel ToModel()
    {
        return new ChatMessageModel
        {
            Id = Id,
            Role = Role,
            Content = Content,
            Timestamp = Timestamp,
            TokenCount = TokenCount
        };
    }
    
    /// <summary>
    /// Appends text during streaming.
    /// </summary>
    public void AppendContent(string text)
    {
        _contentBuilder.Append(text);
        Content = _contentBuilder.ToString();
    }
    
    /// <summary>
    /// Starts streaming mode for this message.
    /// </summary>
    public void StartStreaming()
    {
        IsStreaming = true;
        _contentBuilder.Clear();
        Content = string.Empty;
    }
    
    /// <summary>
    /// Ends streaming mode for this message.
    /// </summary>
    public void EndStreaming()
    {
        IsStreaming = false;
    }
}

