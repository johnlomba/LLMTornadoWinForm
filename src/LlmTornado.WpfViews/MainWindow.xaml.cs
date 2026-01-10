using System.Windows;
using LlmTornado.WpfViews.Services;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Create services
        var chatService = new ChatService();
        var conversationStore = new ConversationStore();
        var promptTemplateService = new PromptTemplateService();
        
        // Create and set view model
        _viewModel = new MainViewModel(chatService, conversationStore, promptTemplateService);
        DataContext = _viewModel;
        
        // Initialize on load
        Loaded += OnLoaded;
    }
    
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
    
    private void SettingsDialog_CloseRequested(object sender, EventArgs e)
    {
        _viewModel.CloseSettingsCommand.Execute(null);
    }
}

