using System.Text.Json.Serialization;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Represents a saved conversation with messages.
/// </summary>
public class ConversationModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? SystemPromptTemplateId { get; set; }
    public string? CustomSystemPrompt { get; set; }
    public string ModelId { get; set; } = "gpt-4";
    public List<ChatMessageModel> Messages { get; set; } = [];
}

/// <summary>
/// Represents a single message in a conversation.
/// </summary>
public class ChatMessageModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? TokenCount { get; set; }
    public List<MessageAttachmentModel>? Attachments { get; set; }
    
    [JsonIgnore]
    public bool IsStreaming { get; set; }
}

/// <summary>
/// Represents a serialized attachment in a message.
/// </summary>
public class MessageAttachmentModel
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public FileAttachmentType FileType { get; set; }
    public string FileSizeDisplay { get; set; } = string.Empty;
    public string? ThumbnailBase64 { get; set; }
}

/// <summary>
/// Message role enumeration.
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant
}
