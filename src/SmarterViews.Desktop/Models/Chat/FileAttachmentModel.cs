namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Type of file attachment for categorization and processing.
/// </summary>
public enum FileAttachmentType
{
    /// <summary>Image file (PNG, JPEG, GIF, WebP) for vision models.</summary>
    Image,
    
    /// <summary>PDF document.</summary>
    Document,
    
    /// <summary>Text-based source code or configuration file.</summary>
    Code,
    
    /// <summary>Plain text file.</summary>
    Text
}

/// <summary>
/// Status of file attachment processing.
/// </summary>
public enum FileAttachmentStatus
{
    /// <summary>File is pending upload/processing.</summary>
    Pending,
    
    /// <summary>File is currently being processed.</summary>
    Processing,
    
    /// <summary>File has been successfully processed and is ready.</summary>
    Ready,
    
    /// <summary>An error occurred during processing.</summary>
    Error
}

/// <summary>
/// Represents a file attached to a chat message.
/// Supports images for vision models, documents, and text/code files.
/// </summary>
public class FileAttachmentModel
{
    /// <summary>Unique identifier for this attachment.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Full path to the file on disk.</summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>Display name of the file.</summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>Categorized type of the file.</summary>
    public FileAttachmentType FileType { get; set; }
    
    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }
    
    /// <summary>MIME type of the file.</summary>
    public string MimeType { get; set; } = string.Empty;
    
    /// <summary>Base64-encoded content for API transmission.</summary>
    public string? Base64Content { get; set; }
    
    /// <summary>Plain text content for text/code files.</summary>
    public string? TextContent { get; set; }
    
    /// <summary>Current processing status.</summary>
    public FileAttachmentStatus Status { get; set; } = FileAttachmentStatus.Pending;
    
    /// <summary>Error message if processing failed.</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>When the file was attached.</summary>
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Maximum allowed file size (20 MB).</summary>
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    
    /// <summary>Maximum file size for text files (1 MB to avoid context overflow).</summary>
    public const long MaxTextFileSizeBytes = 1 * 1024 * 1024;
    
    /// <summary>Supported image extensions for vision models.</summary>
    public static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];
    
    /// <summary>Supported document extensions.</summary>
    public static readonly string[] SupportedDocumentExtensions = [".pdf"];
    
    /// <summary>Supported code/source file extensions.</summary>
    public static readonly string[] SupportedCodeExtensions = [
        ".cs", ".fs", ".vb",           // .NET
        ".js", ".ts", ".jsx", ".tsx",  // JavaScript/TypeScript
        ".py", ".pyw",                 // Python
        ".java", ".kt", ".scala",      // JVM
        ".c", ".cpp", ".h", ".hpp",    // C/C++
        ".go", ".rs", ".swift",        // Modern systems
        ".rb", ".php", ".pl",          // Scripting
        ".sql", ".graphql",            // Query languages
        ".html", ".htm", ".css", ".scss", ".sass", ".less", // Web
        ".xml", ".xaml", ".json", ".yaml", ".yml", ".toml", // Config
        ".sh", ".bash", ".ps1", ".cmd", ".bat",  // Shell
        ".md", ".markdown", ".rst",    // Documentation
        ".dockerfile", ".gitignore", ".editorconfig" // DevOps
    ];
    
    /// <summary>Supported plain text extensions.</summary>
    public static readonly string[] SupportedTextExtensions = [".txt", ".log", ".csv", ".tsv", ".ini", ".cfg", ".conf"];
    
    /// <summary>All supported file extensions.</summary>
    public static readonly string[] AllSupportedExtensions = [
        ..SupportedImageExtensions, 
        ..SupportedDocumentExtensions,
        ..SupportedCodeExtensions,
        ..SupportedTextExtensions
    ];
    
    /// <summary>File dialog filter string for Open File dialogs.</summary>
    public static string FileDialogFilter => 
        "All Supported Files|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.pdf;*.cs;*.js;*.ts;*.py;*.json;*.xml;*.yaml;*.md;*.txt;*.sql|" +
        "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|" +
        "Code Files|*.cs;*.js;*.ts;*.jsx;*.tsx;*.py;*.java;*.cpp;*.c;*.go;*.rs;*.rb|" +
        "Config Files|*.json;*.xml;*.yaml;*.yml;*.toml;*.ini|" +
        "Text Files|*.txt;*.md;*.log;*.csv|" +
        "PDF Documents|*.pdf";
    
    /// <summary>
    /// Determines the file type based on extension.
    /// </summary>
    /// <param name="extension">File extension including the dot.</param>
    /// <returns>The file type, or null if not supported.</returns>
    public static FileAttachmentType? GetFileType(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (SupportedImageExtensions.Contains(ext))
            return FileAttachmentType.Image;
        if (SupportedDocumentExtensions.Contains(ext))
            return FileAttachmentType.Document;
        if (SupportedCodeExtensions.Contains(ext))
            return FileAttachmentType.Code;
        if (SupportedTextExtensions.Contains(ext))
            return FileAttachmentType.Text;
        return null;
    }
    
    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    /// <param name="extension">File extension including the dot.</param>
    /// <returns>MIME type string.</returns>
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
            ".json" => "application/json",
            ".xml" or ".xaml" => "application/xml",
            ".yaml" or ".yml" => "application/x-yaml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".ts" => "text/typescript",
            ".md" or ".markdown" => "text/markdown",
            ".csv" => "text/csv",
            _ => "text/plain"
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
    /// Checks if the file type is text-based (can be read as string).
    /// </summary>
    public bool IsTextBased => FileType is FileAttachmentType.Code or FileAttachmentType.Text;
    
    /// <summary>
    /// Validates the file and returns any validation errors.
    /// </summary>
    /// <returns>Validation error message, or null if valid.</returns>
    public string? Validate()
    {
        if (string.IsNullOrEmpty(FilePath))
            return "File path is required";
            
        if (!System.IO.File.Exists(FilePath))
            return "File does not exist";
            
        var extension = System.IO.Path.GetExtension(FilePath);
        if (!IsSupportedExtension(extension))
            return $"File type '{extension}' is not supported";
            
        if (IsTextBased && FileSize > MaxTextFileSizeBytes)
            return $"Text file exceeds maximum size of {MaxTextFileSizeBytes / 1024 / 1024} MB";
            
        if (FileSize > MaxFileSizeBytes)
            return $"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024} MB";
            
        return null;
    }
    
    /// <summary>
    /// Gets a human-readable file size display.
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
    /// Gets a preview snippet of text content (first 200 chars).
    /// </summary>
    public string? TextPreview => TextContent?.Length > 200 
        ? TextContent[..200] + "..." 
        : TextContent;
}
