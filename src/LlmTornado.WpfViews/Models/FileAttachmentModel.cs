using System.IO;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Type of file attachment.
/// </summary>
public enum FileAttachmentType
{
    /// <summary>
    /// Image file (PNG, JPG, JPEG, GIF, WebP).
    /// </summary>
    Image,
    
    /// <summary>
    /// PDF document (Anthropic only).
    /// </summary>
    Document,
    
    /// <summary>
    /// Text file (for code or plain text content).
    /// </summary>
    Text,
    
    /// <summary>
    /// Unknown or unsupported file type.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents a file attached to a chat message.
/// Supports images, PDFs, and text files with automatic type detection and validation.
/// </summary>
/// <remarks>
/// File attachments are processed as follows:
/// - Images: Converted to Base64 and sent as data URLs
/// - PDFs: Sent as ChatDocument (Anthropic only)
/// - Text: Content extracted and included as text
/// </remarks>
public class FileAttachmentModel
{
    /// <summary>
    /// Unique identifier for the attachment.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Full path to the file on disk.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of the attachment (Image, Document, Text).
    /// </summary>
    public FileAttachmentType FileType { get; set; }
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// MIME type of the file.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Base64-encoded content of the file (cached for sending).
    /// </summary>
    public string? Base64Content { get; set; }
    
    /// <summary>
    /// Plain text content (for text files).
    /// </summary>
    public string? TextContent { get; set; }
    
    /// <summary>
    /// Whether this attachment has been uploaded/processed.
    /// </summary>
    public bool IsProcessed { get; set; }
    
    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Timestamp when the attachment was added.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Maximum file size allowed (20 MB).
    /// </summary>
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    
    /// <summary>
    /// Maximum image dimension (for resizing).
    /// </summary>
    public const int MaxImageDimension = 2048;
    
    /// <summary>
    /// Supported image extensions.
    /// </summary>
    public static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];
    
    /// <summary>
    /// Supported document extensions.
    /// </summary>
    public static readonly string[] SupportedDocumentExtensions = [".pdf"];
    
    /// <summary>
    /// Supported text file extensions.
    /// </summary>
    public static readonly string[] SupportedTextExtensions = [
        ".txt", ".md", ".markdown", ".json", ".xml", ".yaml", ".yml",
        ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
        ".html", ".htm", ".css", ".scss", ".less",
        ".sql", ".sh", ".bash", ".ps1", ".psm1",
        ".gitignore", ".env", ".config", ".ini"
    ];
    
    /// <summary>
    /// All supported file extensions.
    /// </summary>
    public static readonly string[] AllSupportedExtensions = 
        [..SupportedImageExtensions, ..SupportedDocumentExtensions, ..SupportedTextExtensions];
    
    /// <summary>
    /// Gets the file filter string for OpenFileDialog.
    /// </summary>
    public static string FileDialogFilter => 
        "All Supported Files|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.pdf;*.txt;*.md;*.json;*.cs;*.js;*.py;*.sql|" +
        "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|" +
        "PDF Documents|*.pdf|" +
        "Text & Code|*.txt;*.md;*.json;*.cs;*.js;*.ts;*.py;*.sql";
    
    /// <summary>
    /// Determines the file type from a file extension.
    /// </summary>
    public static FileAttachmentType GetFileType(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (SupportedImageExtensions.Contains(ext))
            return FileAttachmentType.Image;
        if (SupportedDocumentExtensions.Contains(ext))
            return FileAttachmentType.Document;
        if (SupportedTextExtensions.Contains(ext))
            return FileAttachmentType.Text;
        return FileAttachmentType.Unknown;
    }
    
    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    public static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".md" or ".markdown" => "text/markdown",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "text/yaml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".ts" => "text/typescript",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".java" => "text/x-java",
            ".sql" => "text/x-sql",
            _ => "application/octet-stream"
        };
    }
    
    /// <summary>
    /// Checks if a file extension is supported.
    /// </summary>
    public static bool IsSupportedExtension(string extension)
    {
        return AllSupportedExtensions.Contains(extension.ToLowerInvariant());
    }
    
    /// <summary>
    /// Validates a file and returns validation result.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns>Tuple of (isValid, errorMessage).</returns>
    public static (bool IsValid, string? ErrorMessage) ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (false, "File does not exist.");
        }
        
        var extension = Path.GetExtension(filePath);
        if (!IsSupportedExtension(extension))
        {
            return (false, $"File type '{extension}' is not supported.");
        }
        
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            return (false, $"File size ({fileInfo.Length / (1024 * 1024):F1} MB) exceeds the maximum allowed ({MaxFileSizeBytes / (1024 * 1024)} MB).");
        }
        
        return (true, null);
    }
    
    /// <summary>
    /// Creates a FileAttachmentModel from a file path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The created attachment model, or null if the file is invalid.</returns>
    public static FileAttachmentModel? CreateFromFile(string filePath)
    {
        var (isValid, errorMessage) = ValidateFile(filePath);
        if (!isValid)
        {
            return null;
        }
        
        var fileInfo = new FileInfo(filePath);
        var extension = Path.GetExtension(filePath);
        
        return new FileAttachmentModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            FileType = GetFileType(extension),
            FileSize = fileInfo.Length,
            MimeType = GetMimeType(extension),
            AddedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Loads the file content into memory (Base64 for binary, text for text files).
    /// </summary>
    public async Task LoadContentAsync()
    {
        if (IsProcessed) return;
        
        try
        {
            if (FileType == FileAttachmentType.Text)
            {
                TextContent = await File.ReadAllTextAsync(FilePath);
            }
            else
            {
                var bytes = await File.ReadAllBytesAsync(FilePath);
                Base64Content = Convert.ToBase64String(bytes);
            }
            
            IsProcessed = true;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsProcessed = false;
        }
    }
    
    /// <summary>
    /// Gets a human-readable file size string.
    /// </summary>
    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
    
    /// <summary>
    /// Gets an icon character for the file type.
    /// </summary>
    public string FileTypeIcon => FileType switch
    {
        FileAttachmentType.Image => "🖼️",
        FileAttachmentType.Document => "📄",
        FileAttachmentType.Text => "📝",
        _ => "📎"
    };
}

