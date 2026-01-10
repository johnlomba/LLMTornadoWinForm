using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// Main view model for the application window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    private readonly ConversationStore _conversationStore;
    private readonly PromptTemplateService _promptTemplateService;
    
    [ObservableProperty]
    private ChatViewModel _chatViewModel;
    
    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;
    
    [ObservableProperty]
    private SettingsModel _currentSettings = new();
    
    [ObservableProperty]
    private PromptTemplate? _selectedPromptTemplate;
    
    [ObservableProperty]
    private ConversationSummary? _selectedConversation;
    
    [ObservableProperty]
    private ModelOption? _selectedModel;
    
    [ObservableProperty]
    private bool _isSettingsOpen;
    
    [ObservableProperty]
    private bool _isInitialized;
    
    [ObservableProperty]
    private string _connectionStatus = "Not connected";
    
    /// <summary>
    /// Available prompt templates.
    /// </summary>
    public ObservableCollection<PromptTemplate> PromptTemplates { get; } = [];
    
    /// <summary>
    /// Available models.
    /// </summary>
    public ObservableCollection<ModelOption> AvailableModels { get; } = new(ModelOption.GetDefaultModels());
    
    /// <summary>
    /// List of saved conversations.
    /// </summary>
    public ObservableCollection<ConversationSummary> Conversations { get; } = [];
    
    public MainViewModel(
        ChatService chatService,
        ConversationStore conversationStore,
        PromptTemplateService promptTemplateService)
    {
        _chatService = chatService;
        _conversationStore = conversationStore;
        _promptTemplateService = promptTemplateService;
        
        ChatViewModel = new ChatViewModel(chatService, conversationStore);
        SettingsViewModel = new SettingsViewModel(conversationStore);
        
        SettingsViewModel.SettingsSaved += OnSettingsSaved;
        ChatViewModel.ConversationSaved += OnConversationSaved;
    }
    
    /// <summary>
    /// Handles conversation being saved - refresh the list.
    /// </summary>
    private async void OnConversationSaved()
    {
        await RefreshConversationsAsync();
    }
    
    /// <summary>
    /// Initializes the application.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Load settings
        await SettingsViewModel.LoadSettingsAsync();
        CurrentSettings = SettingsViewModel.ToModel();
        
        // Set selected model
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == CurrentSettings.SelectedModelId)
            ?? AvailableModels.First();
        
        // Load prompt templates
        var templates = await _promptTemplateService.GetAllTemplatesAsync();
        PromptTemplates.Clear();
        foreach (var template in templates)
        {
            PromptTemplates.Add(template);
        }
        
        // Set default template
        SelectedPromptTemplate = PromptTemplates.FirstOrDefault(t => t.Id == CurrentSettings.DefaultPromptTemplateId)
            ?? PromptTemplates.FirstOrDefault();
        
        // Load conversations
        await RefreshConversationsAsync();
        
        // Try to initialize chat if API key is set
        if (!string.IsNullOrWhiteSpace(CurrentSettings.OpenAiApiKey) || CurrentSettings.UseAzure)
        {
            await ConnectAsync();
        }
    }
    
    /// <summary>
    /// Connects to the AI service.
    /// </summary>
    [RelayCommand]
    public async Task ConnectAsync()
    {
        try
        {
            var systemPrompt = SelectedPromptTemplate?.Content;
            ChatViewModel.Initialize(CurrentSettings, systemPrompt);
            
            if (ChatViewModel.IsInitialized)
            {
                IsInitialized = true;
                ConnectionStatus = "Connected";
                ChatViewModel.NewConversation();
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
            IsInitialized = false;
        }
    }
    
    /// <summary>
    /// Opens the settings dialog.
    /// </summary>
    [RelayCommand]
    public void OpenSettings()
    {
        IsSettingsOpen = true;
    }
    
    /// <summary>
    /// Closes the settings dialog.
    /// </summary>
    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsOpen = false;
    }
    
    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    [RelayCommand]
    public void NewConversation()
    {
        SelectedConversation = null;
        ChatViewModel.NewConversation();
    }
    
    /// <summary>
    /// Loads the selected conversation.
    /// </summary>
    [RelayCommand]
    public async Task SelectConversationAsync(ConversationSummary? conversation)
    {
        if (conversation == null)
            return;
        
        SelectedConversation = conversation;
        await ChatViewModel.LoadConversationAsync(conversation.Id);
    }
    
    /// <summary>
    /// Deletes a conversation.
    /// </summary>
    [RelayCommand]
    public async Task DeleteConversationAsync(ConversationSummary? conversation)
    {
        if (conversation == null)
            return;
        
        await _conversationStore.DeleteConversationAsync(conversation.Id);
        Conversations.Remove(conversation);
        
        if (SelectedConversation?.Id == conversation.Id)
        {
            NewConversation();
        }
    }
    
    /// <summary>
    /// Refreshes the list of conversations.
    /// </summary>
    public async Task RefreshConversationsAsync()
    {
        var summaries = await _conversationStore.GetConversationSummariesAsync();
        Conversations.Clear();
        foreach (var summary in summaries)
        {
            Conversations.Add(summary);
        }
    }
    
    /// <summary>
    /// Updates the system prompt when template changes.
    /// </summary>
    partial void OnSelectedPromptTemplateChanged(PromptTemplate? value)
    {
        if (value != null && IsInitialized)
        {
            ChatViewModel.SystemPromptContent = value.Content;
            _chatService.UpdateSystemPrompt(CurrentSettings, value.Content);
        }
    }
    
    /// <summary>
    /// Updates the model when selection changes.
    /// </summary>
    partial void OnSelectedModelChanged(ModelOption? value)
    {
        if (value != null)
        {
            CurrentSettings.SelectedModelId = value.Id;
            
            if (IsInitialized)
            {
                // Reconnect with new model
                _ = ConnectAsync();
            }
        }
    }
    
    /// <summary>
    /// Handles settings being saved.
    /// </summary>
    private async void OnSettingsSaved(SettingsModel settings)
    {
        CurrentSettings = settings;
        
        // Update selected model
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == settings.SelectedModelId)
            ?? AvailableModels.First();
        
        // Reconnect with new settings
        await ConnectAsync();
        
        CloseSettings();
    }
}

