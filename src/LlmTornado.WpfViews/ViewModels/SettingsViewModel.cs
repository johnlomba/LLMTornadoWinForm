using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the settings dialog.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConversationStore _conversationStore;
    
    [ObservableProperty]
    private string _openAiApiKey = string.Empty;
    
    [ObservableProperty]
    private string _azureEndpoint = string.Empty;
    
    [ObservableProperty]
    private string _azureApiKey = string.Empty;
    
    [ObservableProperty]
    private bool _useAzure;
    
    [ObservableProperty]
    private ModelOption? _selectedModel;
    
    [ObservableProperty]
    private bool _enableStreaming = true;
    
    [ObservableProperty]
    private int _maxTokens = 4096;
    
    [ObservableProperty]
    private double _temperature = 0.7;
    
    [ObservableProperty]
    private bool _saveConversationHistory = true;
    
    [ObservableProperty]
    private string _theme = "Dark";
    
    [ObservableProperty]
    private bool _hasChanges;
    
    [ObservableProperty]
    private string? _validationError;
    
    /// <summary>
    /// Available model options.
    /// </summary>
    public ObservableCollection<ModelOption> AvailableModels { get; } = new(ModelOption.GetDefaultModels());
    
    /// <summary>
    /// Available themes.
    /// </summary>
    public ObservableCollection<string> AvailableThemes { get; } = ["Dark", "Light"];
    
    /// <summary>
    /// Event raised when settings are saved.
    /// </summary>
    public event Action<SettingsModel>? SettingsSaved;
    
    public SettingsViewModel(ConversationStore conversationStore)
    {
        _conversationStore = conversationStore;
        
        // Set default model
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == "gpt-4");
    }
    
    /// <summary>
    /// Loads settings from storage.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        var settings = await _conversationStore.LoadSettingsAsync();
        
        OpenAiApiKey = settings.OpenAiApiKey ?? string.Empty;
        AzureEndpoint = settings.AzureEndpoint ?? string.Empty;
        AzureApiKey = settings.AzureApiKey ?? string.Empty;
        UseAzure = settings.UseAzure;
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == settings.SelectedModelId) 
            ?? AvailableModels.First();
        EnableStreaming = settings.EnableStreaming;
        MaxTokens = settings.MaxTokens;
        Temperature = settings.Temperature;
        SaveConversationHistory = settings.SaveConversationHistory;
        Theme = settings.Theme;
        
        HasChanges = false;
    }
    
    /// <summary>
    /// Saves settings to storage.
    /// </summary>
    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (!Validate())
            return;
        
        var settings = ToModel();
        await _conversationStore.SaveSettingsAsync(settings);
        
        HasChanges = false;
        SettingsSaved?.Invoke(settings);
    }
    
    /// <summary>
    /// Validates the current settings.
    /// </summary>
    public bool Validate()
    {
        ValidationError = null;
        
        if (!UseAzure && string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            ValidationError = "OpenAI API key is required.";
            return false;
        }
        
        if (UseAzure)
        {
            if (string.IsNullOrWhiteSpace(AzureEndpoint))
            {
                ValidationError = "Azure endpoint is required when using Azure.";
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(AzureApiKey))
            {
                ValidationError = "Azure API key is required when using Azure.";
                return false;
            }
        }
        
        if (MaxTokens < 1 || MaxTokens > 128000)
        {
            ValidationError = "Max tokens must be between 1 and 128000.";
            return false;
        }
        
        if (Temperature < 0 || Temperature > 2)
        {
            ValidationError = "Temperature must be between 0 and 2.";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Converts the view model to a settings model.
    /// </summary>
    public SettingsModel ToModel()
    {
        return new SettingsModel
        {
            OpenAiApiKey = OpenAiApiKey,
            AzureEndpoint = AzureEndpoint,
            AzureApiKey = AzureApiKey,
            UseAzure = UseAzure,
            SelectedModelId = SelectedModel?.Id ?? "gpt-4",
            EnableStreaming = EnableStreaming,
            MaxTokens = MaxTokens,
            Temperature = Temperature,
            SaveConversationHistory = SaveConversationHistory,
            Theme = Theme
        };
    }
    
    partial void OnOpenAiApiKeyChanged(string value) => HasChanges = true;
    partial void OnAzureEndpointChanged(string value) => HasChanges = true;
    partial void OnAzureApiKeyChanged(string value) => HasChanges = true;
    partial void OnUseAzureChanged(bool value) => HasChanges = true;
    partial void OnSelectedModelChanged(ModelOption? value) => HasChanges = true;
    partial void OnEnableStreamingChanged(bool value) => HasChanges = true;
    partial void OnMaxTokensChanged(int value) => HasChanges = true;
    partial void OnTemperatureChanged(double value) => HasChanges = true;
    partial void OnSaveConversationHistoryChanged(bool value) => HasChanges = true;
    partial void OnThemeChanged(string value) => HasChanges = true;
}

