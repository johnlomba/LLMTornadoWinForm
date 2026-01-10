using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.ViewModels.Chat;

/// <summary>
/// ViewModel for a file attachment with thumbnail preview, text preview, and remove functionality.
/// Supports images (with thumbnails), documents, and text/code files (with text preview).
/// </summary>
public partial class FileAttachmentViewModel : ObservableObject
{
    private readonly FileAttachmentModel _model;
    
    /// <summary>Thumbnail image for image files.</summary>
    [ObservableProperty]
    private ImageSource? _thumbnail;
    
    /// <summary>Whether the file is currently being loaded.</summary>
    [ObservableProperty]
    private bool _isLoading = true;
    
    /// <summary>Loading progress percentage (0-100).</summary>
    [ObservableProperty]
    private int _loadingProgress;
    
    /// <summary>Error message if loading failed.</summary>
    [ObservableProperty]
    private string? _errorMessage;
    
    /// <summary>Preview of text content for text/code files.</summary>
    [ObservableProperty]
    private string? _textPreview;
    
    /// <summary>Language hint for syntax highlighting (based on extension).</summary>
    [ObservableProperty]
    private string? _languageHint;
    
    /// <summary>Event raised when the user requests to remove this attachment.</summary>
    public event Action<FileAttachmentViewModel>? RemoveRequested;
    
    /// <summary>
    /// Creates a new FileAttachmentViewModel from a file path.
    /// </summary>
    /// <param name="filePath">Path to the file to attach.</param>
    /// <exception cref="ArgumentException">Thrown if the file type is not supported.</exception>
    public FileAttachmentViewModel(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var extension = fileInfo.Extension.ToLowerInvariant();
        var fileType = FileAttachmentModel.GetFileType(extension);
        
        if (fileType == null)
        {
            throw new ArgumentException($"Unsupported file type: {extension}");
        }
        
        _model = new FileAttachmentModel
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileType = fileType.Value,
            FileSize = fileInfo.Length,
            MimeType = FileAttachmentModel.GetMimeType(extension)
        };
        
        // Set language hint for code files
        LanguageHint = GetLanguageFromExtension(extension);
        
        _ = LoadAsync();
    }
    
    /// <summary>
    /// Creates a new FileAttachmentViewModel from an existing model.
    /// </summary>
    /// <param name="model">The file attachment model.</param>
    public FileAttachmentViewModel(FileAttachmentModel model)
    {
        _model = model;
        LanguageHint = GetLanguageFromExtension(Path.GetExtension(model.FilePath));
        _ = LoadAsync();
    }
    
    /// <summary>The underlying model.</summary>
    public FileAttachmentModel Model => _model;
    
    /// <summary>Unique identifier.</summary>
    public string Id => _model.Id;
    
    /// <summary>Display name of the file.</summary>
    public string FileName => _model.FileName;
    
    /// <summary>Full path to the file.</summary>
    public string FilePath => _model.FilePath;
    
    /// <summary>Type of file.</summary>
    public FileAttachmentType FileType => _model.FileType;
    
    /// <summary>Human-readable file size.</summary>
    public string FileSizeDisplay => _model.FileSizeDisplay;
    
    /// <summary>File size in bytes.</summary>
    public long FileSize => _model.FileSize;
    
    /// <summary>Whether this is an image file.</summary>
    public bool IsImage => _model.FileType == FileAttachmentType.Image;
    
    /// <summary>Whether this is a PDF document.</summary>
    public bool IsDocument => _model.FileType == FileAttachmentType.Document;
    
    /// <summary>Whether this is a text or code file.</summary>
    public bool IsTextBased => _model.IsTextBased;
    
    /// <summary>Whether this is a code file (for syntax highlighting).</summary>
    public bool IsCode => _model.FileType == FileAttachmentType.Code;
    
    /// <summary>Base64-encoded content for binary files.</summary>
    public string? Base64Content => _model.Base64Content;
    
    /// <summary>Text content for text-based files.</summary>
    public string? TextContent => _model.TextContent;
    
    /// <summary>MIME type of the file.</summary>
    public string MimeType => _model.MimeType;
    
    /// <summary>Whether the file exceeds the maximum allowed size.</summary>
    public bool IsFileTooLarge => IsTextBased 
        ? _model.FileSize > FileAttachmentModel.MaxTextFileSizeBytes
        : _model.FileSize > FileAttachmentModel.MaxFileSizeBytes;
    
    /// <summary>Current processing status.</summary>
    public FileAttachmentStatus Status => _model.Status;
    
    /// <summary>Whether the file is ready for use.</summary>
    public bool IsReady => _model.Status == FileAttachmentStatus.Ready;
    
    /// <summary>Whether there was an error processing the file.</summary>
    public bool HasError => _model.Status == FileAttachmentStatus.Error;
    
    /// <summary>
    /// Removes this attachment from the parent collection.
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this);
    }
    
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            ErrorMessage = null;
            _model.Status = FileAttachmentStatus.Processing;
            
            // Validate the file first
            var validationError = _model.Validate();
            if (validationError != null)
            {
                ErrorMessage = validationError;
                _model.Status = FileAttachmentStatus.Error;
                _model.ErrorMessage = validationError;
                return;
            }
            
            LoadingProgress = 25;
            
            if (IsTextBased)
            {
                // Read as text for code/text files
                var text = await File.ReadAllTextAsync(_model.FilePath);
                _model.TextContent = text;
                TextPreview = _model.TextPreview;
                LoadingProgress = 75;
            }
            else
            {
                // Read as binary for images/documents
                var bytes = await File.ReadAllBytesAsync(_model.FilePath);
                _model.Base64Content = Convert.ToBase64String(bytes);
                LoadingProgress = 50;
                
                await GenerateThumbnailAsync(bytes);
            }
            
            LoadingProgress = 100;
            _model.Status = FileAttachmentStatus.Ready;
            OnPropertyChanged(nameof(IsReady));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load: {ex.Message}";
            _model.Status = FileAttachmentStatus.Error;
            _model.ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(HasError));
        }
    }
    
    private async Task GenerateThumbnailAsync(byte[] fileBytes)
    {
        await Task.Run(() =>
        {
            try
            {
                if (_model.FileType == FileAttachmentType.Image)
                {
                    var bitmap = new BitmapImage();
                    using var ms = new MemoryStream(fileBytes);
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.DecodePixelWidth = 100;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = bitmap;
                    });
                }
                else if (_model.FileType == FileAttachmentType.Document)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = null;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ErrorMessage = $"Thumbnail failed: {ex.Message}";
                });
            }
        });
    }
    
    /// <summary>
    /// Gets a language identifier from file extension for syntax highlighting.
    /// </summary>
    private static string? GetLanguageFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".fs" => "fsharp",
            ".vb" => "vb",
            ".js" or ".jsx" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".py" or ".pyw" => "python",
            ".java" => "java",
            ".kt" => "kotlin",
            ".scala" => "scala",
            ".c" => "c",
            ".cpp" or ".hpp" => "cpp",
            ".h" => "c",
            ".go" => "go",
            ".rs" => "rust",
            ".swift" => "swift",
            ".rb" => "ruby",
            ".php" => "php",
            ".pl" => "perl",
            ".sql" => "sql",
            ".graphql" => "graphql",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".scss" or ".sass" => "scss",
            ".less" => "less",
            ".xml" or ".xaml" => "xml",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".toml" => "toml",
            ".sh" or ".bash" => "bash",
            ".ps1" => "powershell",
            ".cmd" or ".bat" => "batch",
            ".md" or ".markdown" => "markdown",
            ".dockerfile" => "dockerfile",
            _ => null
        };
    }
    
    /// <summary>
    /// Validates a file before creating an attachment.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns>Tuple of (IsValid, ErrorMessage).</returns>
    public static (bool IsValid, string? Error) ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return (false, "File does not exist");
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!FileAttachmentModel.IsSupportedExtension(extension))
            return (false, $"Unsupported file type: {extension}");
        
        var fileInfo = new FileInfo(filePath);
        var fileType = FileAttachmentModel.GetFileType(extension);
        
        // Use appropriate size limit based on file type
        var maxSize = fileType is FileAttachmentType.Code or FileAttachmentType.Text
            ? FileAttachmentModel.MaxTextFileSizeBytes
            : FileAttachmentModel.MaxFileSizeBytes;
            
        if (fileInfo.Length > maxSize)
            return (false, $"File too large (max {maxSize / (1024 * 1024)}MB)");
        
        return (true, null);
    }
    
    /// <summary>
    /// Gets an icon name/glyph for the file type (for UI display).
    /// </summary>
    public string FileTypeIcon => FileType switch
    {
        FileAttachmentType.Image => "🖼️",
        FileAttachmentType.Document => "📄",
        FileAttachmentType.Code => "💻",
        FileAttachmentType.Text => "📝",
        _ => "📎"
    };
}
