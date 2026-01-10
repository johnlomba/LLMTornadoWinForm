using System.Text.Json.Serialization;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Represents a saved conversation with messages.
/// </summary>
public class ConversationModel
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Display title for the conversation (usually first message or user-defined).
    /// </summary>
    public string Title { get; set; } = "New Chat";
    
    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The system prompt template ID used for this conversation.
    /// </summary>
    public string? SystemPromptTemplateId { get; set; }
    
    /// <summary>
    /// Custom system prompt if not using a template.
    /// </summary>
    public string? CustomSystemPrompt { get; set; }
    
    /// <summary>
    /// The model used for this conversation.
    /// </summary>
    public string ModelId { get; set; } = "gpt-4";
    
    /// <summary>
    /// Messages in this conversation.
    /// </summary>
    public List<ChatMessageModel> Messages { get; set; } = [];
}

/// <summary>
/// Represents a single message in a conversation.
/// </summary>
public class ChatMessageModel
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Role of the message sender (user, assistant, system).
    /// </summary>
    public MessageRole Role { get; set; }
    
    /// <summary>
    /// Content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Token count for this message (if available).
    /// </summary>
    public int? TokenCount { get; set; }
    
    /// <summary>
    /// Attachments for this message.
    /// </summary>
    public List<MessageAttachmentModel>? Attachments { get; set; }
    
    /// <summary>
    /// Whether this message is currently being streamed.
    /// </summary>
    [JsonIgnore]
    public bool IsStreaming { get; set; }
}

/// <summary>
/// Represents a serialized attachment in a message.
/// </summary>
public class MessageAttachmentModel
{
    /// <summary>
    /// Unique identifier for the attachment.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the attachment.
    /// </summary>
    public FileAttachmentType FileType { get; set; }
    
    /// <summary>
    /// Human-readable file size.
    /// </summary>
    public string FileSizeDisplay { get; set; } = string.Empty;
    
    /// <summary>
    /// Base64-encoded thumbnail for images (for display in history).
    /// </summary>
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

