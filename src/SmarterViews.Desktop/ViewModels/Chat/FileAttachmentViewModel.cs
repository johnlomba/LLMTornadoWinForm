using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.ViewModels.Chat;

/// <summary>
/// ViewModel for a file attachment with thumbnail preview and remove functionality.
/// </summary>
public partial class FileAttachmentViewModel : ObservableObject
{
    private readonly FileAttachmentModel _model;
    
    [ObservableProperty]
    private ImageSource? _thumbnail;
    
    [ObservableProperty]
    private bool _isLoading = true;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    public event Action<FileAttachmentViewModel>? RemoveRequested;
    
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
        
        _ = LoadAsync();
    }
    
    public FileAttachmentViewModel(FileAttachmentModel model)
    {
        _model = model;
        _ = LoadAsync();
    }
    
    public FileAttachmentModel Model => _model;
    public string Id => _model.Id;
    public string FileName => _model.FileName;
    public string FilePath => _model.FilePath;
    public FileAttachmentType FileType => _model.FileType;
    public string FileSizeDisplay => _model.FileSizeDisplay;
    public long FileSize => _model.FileSize;
    public bool IsImage => _model.FileType == FileAttachmentType.Image;
    public bool IsDocument => _model.FileType == FileAttachmentType.Document;
    public string? Base64Content => _model.Base64Content;
    public string MimeType => _model.MimeType;
    public bool IsFileTooLarge => _model.FileSize > FileAttachmentModel.MaxFileSizeBytes;
    
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
            ErrorMessage = null;
            
            if (IsFileTooLarge)
            {
                ErrorMessage = $"File too large (max {FileAttachmentModel.MaxFileSizeBytes / (1024 * 1024)}MB)";
                return;
            }
            
            var bytes = await File.ReadAllBytesAsync(_model.FilePath);
            _model.Base64Content = Convert.ToBase64String(bytes);
            
            await GenerateThumbnailAsync(bytes);
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
