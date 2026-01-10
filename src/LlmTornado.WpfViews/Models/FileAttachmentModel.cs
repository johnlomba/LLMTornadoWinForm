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
    Document
}

/// <summary>
/// Represents a file attached to a chat message.
/// </summary>
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
    /// Type of the attachment (Image or Document).
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
    /// Maximum file size allowed (20 MB).
    /// </summary>
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    
    /// <summary>
    /// Supported image extensions.
    /// </summary>
    public static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
    
    /// <summary>
    /// Supported document extensions.
    /// </summary>
    public static readonly string[] SupportedDocumentExtensions = [".pdf"];
    
    /// <summary>
    /// All supported file extensions.
    /// </summary>
    public static readonly string[] AllSupportedExtensions = [..SupportedImageExtensions, ..SupportedDocumentExtensions];
    
    /// <summary>
    /// Gets the file filter string for OpenFileDialog.
    /// </summary>
    public static string FileDialogFilter => 
        "All Supported Files|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.pdf|" +
        "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp|" +
        "PDF Documents|*.pdf";
    
    /// <summary>
    /// Determines the file type from a file extension.
    /// </summary>
    public static FileAttachmentType? GetFileType(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (SupportedImageExtensions.Contains(ext))
            return FileAttachmentType.Image;
        if (SupportedDocumentExtensions.Contains(ext))
            return FileAttachmentType.Document;
        return null;
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
            ".pdf" => "application/pdf",
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
}

