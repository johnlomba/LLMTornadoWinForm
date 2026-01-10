using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.ViewModels.Chat;

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
    
    public ObservableCollection<MessageAttachmentViewModel> Attachments { get; } = [];
    
    public bool HasAttachments => Attachments.Count > 0;
    
    public string RoleDisplayName => Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "Assistant",
        MessageRole.System => "System",
        _ => "Unknown"
    };
    
    public bool IsUserMessage => Role == MessageRole.User;
    public bool IsAssistantMessage => Role == MessageRole.Assistant;
    
    public static MessageViewModel FromModel(ChatMessageModel model)
    {
        var vm = new MessageViewModel
        {
            Id = model.Id,
            Role = model.Role,
            Content = model.Content,
            Timestamp = model.Timestamp,
            TokenCount = model.TokenCount
        };
        
        if (model.Attachments != null)
        {
            foreach (var attachment in model.Attachments)
            {
                vm.Attachments.Add(MessageAttachmentViewModel.FromModel(attachment));
            }
        }
        
        return vm;
    }
    
    public ChatMessageModel ToModel()
    {
        var model = new ChatMessageModel
        {
            Id = Id,
            Role = Role,
            Content = Content,
            Timestamp = Timestamp,
            TokenCount = TokenCount
        };
        
        if (Attachments.Count > 0)
        {
            model.Attachments = Attachments.Select(a => a.ToModel()).ToList();
        }
        
        return model;
    }
    
    public void AddAttachment(FileAttachmentModel attachment)
    {
        Attachments.Add(MessageAttachmentViewModel.FromFileAttachment(attachment));
        OnPropertyChanged(nameof(HasAttachments));
    }
    
    public void AppendContent(string text)
    {
        _contentBuilder.Append(text);
        Content = _contentBuilder.ToString();
    }
    
    public void StartStreaming()
    {
        IsStreaming = true;
        _contentBuilder.Clear();
        Content = string.Empty;
    }
    
    public void EndStreaming()
    {
        IsStreaming = false;
    }
}

/// <summary>
/// ViewModel for displaying an attachment in a message bubble.
/// </summary>
public partial class MessageAttachmentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _fileName = string.Empty;
    
    [ObservableProperty]
    private FileAttachmentType _fileType;
    
    [ObservableProperty]
    private string _fileSizeDisplay = string.Empty;
    
    [ObservableProperty]
    private ImageSource? _thumbnail;
    
    public bool IsImage => FileType == FileAttachmentType.Image;
    public bool IsDocument => FileType == FileAttachmentType.Document;
    
    public static MessageAttachmentViewModel FromFileAttachment(FileAttachmentModel model)
    {
        var vm = new MessageAttachmentViewModel
        {
            Id = model.Id,
            FileName = model.FileName,
            FileType = model.FileType,
            FileSizeDisplay = model.FileSizeDisplay
        };
        
        if (model.FileType == FileAttachmentType.Image && !string.IsNullOrEmpty(model.Base64Content))
        {
            try
            {
                var bytes = Convert.FromBase64String(model.Base64Content);
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.DecodePixelWidth = 200;
                bitmap.EndInit();
                bitmap.Freeze();
                vm.Thumbnail = bitmap;
            }
            catch
            {
                // Ignore thumbnail generation errors
            }
        }
        
        return vm;
    }
    
    public static MessageAttachmentViewModel FromModel(MessageAttachmentModel model)
    {
        var vm = new MessageAttachmentViewModel
        {
            Id = model.Id,
            FileName = model.FileName,
            FileType = model.FileType,
            FileSizeDisplay = model.FileSizeDisplay
        };
        
        if (model.FileType == FileAttachmentType.Image && !string.IsNullOrEmpty(model.ThumbnailBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(model.ThumbnailBase64);
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                vm.Thumbnail = bitmap;
            }
            catch
            {
                // Ignore thumbnail errors
            }
        }
        
        return vm;
    }
    
    public MessageAttachmentModel ToModel()
    {
        var model = new MessageAttachmentModel
        {
            Id = Id,
            FileName = FileName,
            FileType = FileType,
            FileSizeDisplay = FileSizeDisplay
        };
        
        if (FileType == FileAttachmentType.Image && Thumbnail is BitmapSource bitmapSource)
        {
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                model.ThumbnailBase64 = Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                // Ignore thumbnail save errors
            }
        }
        
        return model;
    }
}
