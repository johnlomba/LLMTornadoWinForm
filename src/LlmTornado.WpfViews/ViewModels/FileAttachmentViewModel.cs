using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for a file attachment with thumbnail preview and remove functionality.
/// Supports images, documents, and text files with async loading.
/// </summary>
/// <remarks>
/// Features:
/// - Automatic thumbnail generation for images
/// - Async content loading with progress indication
/// - Validation of file type and size
/// - Support for drag-and-drop operations
/// </remarks>
public partial class FileAttachmentViewModel : ObservableObject
{
    private readonly FileAttachmentModel _model;
    
    [ObservableProperty]
    private ImageSource? _thumbnail;
    
    [ObservableProperty]
    private bool _isLoading = true;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    [ObservableProperty]
    private double _loadProgress;
    
    [ObservableProperty]
    private string? _textPreview;
    
    /// <summary>
    /// Event raised when this attachment should be removed.
    /// </summary>
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
        
        if (fileType == FileAttachmentType.Unknown)
        {
            throw new ArgumentException($"Unsupported file type: {extension}");
        }
        
        _model = new FileAttachmentModel
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileType = fileType,
            FileSize = fileInfo.Length,
            MimeType = FileAttachmentModel.GetMimeType(extension)
        };
        
        // Load content and thumbnail async
        _ = LoadAsync();
    }
    
    /// <summary>
    /// Creates a FileAttachmentViewModel from an existing model.
    /// </summary>
    /// <param name="model">The existing FileAttachmentModel.</param>
    public FileAttachmentViewModel(FileAttachmentModel model)
    {
        _model = model;
        _ = LoadAsync();
    }
    
    /// <summary>
    /// The underlying model.
    /// </summary>
    public FileAttachmentModel Model => _model;
    
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id => _model.Id;
    
    /// <summary>
    /// Display name of the file.
    /// </summary>
    public string FileName => _model.FileName;
    
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public string FilePath => _model.FilePath;
    
    /// <summary>
    /// Type of attachment.
    /// </summary>
    public FileAttachmentType FileType => _model.FileType;
    
    /// <summary>
    /// Human-readable file size.
    /// </summary>
    public string FileSizeDisplay => _model.FileSizeDisplay;
    
    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize => _model.FileSize;
    
    /// <summary>
    /// Whether this is an image attachment.
    /// </summary>
    public bool IsImage => _model.FileType == FileAttachmentType.Image;
    
    /// <summary>
    /// Whether this is a document attachment.
    /// </summary>
    public bool IsDocument => _model.FileType == FileAttachmentType.Document;
    
    /// <summary>
    /// Whether this is a text file attachment.
    /// </summary>
    public bool IsText => _model.FileType == FileAttachmentType.Text;
    
    /// <summary>
    /// Base64-encoded content for sending to API.
    /// </summary>
    public string? Base64Content => _model.Base64Content;
    
    /// <summary>
    /// Plain text content (for text files).
    /// </summary>
    public string? TextContent => _model.TextContent;
    
    /// <summary>
    /// MIME type of the file.
    /// </summary>
    public string MimeType => _model.MimeType;
    
    /// <summary>
    /// Whether the file exceeds the maximum size.
    /// </summary>
    public bool IsFileTooLarge => _model.FileSize > FileAttachmentModel.MaxFileSizeBytes;
    
    /// <summary>
    /// Gets the file type icon.
    /// </summary>
    public string FileTypeIcon => _model.FileTypeIcon;
    
    /// <summary>
    /// Command to remove this attachment.
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this);
    }
    
    /// <summary>
    /// Command to open the file in the default application.
    /// </summary>
    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _model.FilePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Ignore errors opening file
        }
    }
    
    /// <summary>
    /// Loads the file content and generates thumbnail.
    /// </summary>
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            LoadProgress = 0;
            
            // Validate file size
            if (IsFileTooLarge)
            {
                ErrorMessage = $"File too large (max {FileAttachmentModel.MaxFileSizeBytes / (1024 * 1024)}MB)";
                return;
            }
            
            LoadProgress = 0.3;
            
            // Handle text files differently
            if (_model.FileType == FileAttachmentType.Text)
            {
                _model.TextContent = await File.ReadAllTextAsync(_model.FilePath);
                
                // Generate a preview (first 200 chars)
                TextPreview = _model.TextContent.Length > 200 
                    ? _model.TextContent[..200] + "..." 
                    : _model.TextContent;
                    
                LoadProgress = 1.0;
            }
            else
            {
                // Read file and convert to base64
                var bytes = await File.ReadAllBytesAsync(_model.FilePath);
                LoadProgress = 0.6;
                
                _model.Base64Content = Convert.ToBase64String(bytes);
                LoadProgress = 0.8;
                
                // Generate thumbnail
                await GenerateThumbnailAsync(bytes);
            }
            
            _model.IsProcessed = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// Generates a thumbnail for the attachment.
    /// </summary>
    private async Task GenerateThumbnailAsync(byte[] fileBytes)
    {
        await Task.Run(() =>
        {
            try
            {
                if (_model.FileType == FileAttachmentType.Image)
                {
                    // Create thumbnail from image
                    var bitmap = new BitmapImage();
                    using var ms = new MemoryStream(fileBytes);
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.DecodePixelWidth = 100; // Thumbnail width
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make it thread-safe
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = bitmap;
                    });
                }
                else if (_model.FileType == FileAttachmentType.Document)
                {
                    // For PDFs, we'll use a document icon (set in XAML via DataTrigger)
                    // Just leave thumbnail null for documents
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
    /// Validates that the file can be attached.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return (false, "File does not exist");
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!FileAttachmentModel.IsSupportedExtension(extension))
            return (false, $"Unsupported file type: {extension}");
        
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > FileAttachmentModel.MaxFileSizeBytes)
            return (false, $"File too large (max {FileAttachmentModel.MaxFileSizeBytes / (1024 * 1024)}MB)");
        
        return (true, null);
    }
}

