namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Type of file attachment.
/// </summary>
public enum FileAttachmentType
{
    Image,
    Document
}

/// <summary>
/// Represents a file attached to a chat message.
/// </summary>
public class FileAttachmentModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public FileAttachmentType FileType { get; set; }
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string? Base64Content { get; set; }
    
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    
    public static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];
    public static readonly string[] SupportedDocumentExtensions = [".pdf"];
    public static readonly string[] AllSupportedExtensions = [..SupportedImageExtensions, ..SupportedDocumentExtensions];
    
    public static string FileDialogFilter => 
        "All Supported Files|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.pdf|" +
        "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp|" +
        "PDF Documents|*.pdf";
    
    public static FileAttachmentType? GetFileType(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (SupportedImageExtensions.Contains(ext))
            return FileAttachmentType.Image;
        if (SupportedDocumentExtensions.Contains(ext))
            return FileAttachmentType.Document;
        return null;
    }
    
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
    
    public static bool IsSupportedExtension(string extension)
    {
        return AllSupportedExtensions.Contains(extension.ToLowerInvariant());
    }
    
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
