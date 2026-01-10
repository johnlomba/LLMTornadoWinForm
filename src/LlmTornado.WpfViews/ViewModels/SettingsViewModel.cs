using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.Code;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the settings dialog.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConversationStore _conversationStore;
    private readonly ModelDiscoveryService _modelDiscoveryService;
    
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
    
    [ObservableProperty]
    private bool _isDiscoveringModels;
    
    [ObservableProperty]
    private string? _discoveryStatus;
    
    [ObservableProperty]
    private string _newCustomModelId = string.Empty;
    
    [ObservableProperty]
    private string _newCustomModelDisplayName = string.Empty;
    
    [ObservableProperty]
    private LLmProviders _newCustomModelProvider = LLmProviders.OpenAi;
    
    [ObservableProperty]
    private int _newCustomModelMaxTokens = 4096;
    
    /// <summary>
    /// Provider API key view models.
    /// </summary>
    public ObservableCollection<ProviderApiKeyViewModel> ProviderKeys { get; } = new();
    
    /// <summary>
    /// Available model options (discovered + custom).
    /// </summary>
    public ObservableCollection<ModelOption> AvailableModels { get; } = new();
    
    /// <summary>
    /// Filtered model options for search.
    /// </summary>
    public ObservableCollection<ModelOption> FilteredModels { get; } = new();
    
    [ObservableProperty]
    private string _modelSearchText = string.Empty;
    
    /// <summary>
    /// Custom models defined by the user.
    /// </summary>
    public ObservableCollection<ModelOption> CustomModels { get; } = new();
    
    /// <summary>
    /// Available themes.
    /// </summary>
    public ObservableCollection<string> AvailableThemes { get; } = ["Dark", "Light"];
    
    /// <summary>
    /// Available providers for custom model creation.
    /// </summary>
    public ObservableCollection<LLmProviders> AvailableProviders { get; } = new(ProviderApiKeyModel.GetAllProviders());
    
    /// <summary>
    /// Event raised when settings are saved.
    /// </summary>
    public event Action<SettingsModel>? SettingsSaved;
    
    public SettingsViewModel(ConversationStore conversationStore)
    {
        _conversationStore = conversationStore;
        _modelDiscoveryService = new ModelDiscoveryService();
        
        // Initialize provider keys for all providers
        InitializeProviderKeys();
        
        // Initialize filtered models to match available models
        AvailableModels.CollectionChanged += (s, e) => UpdateFilteredModels();
    }
    
    /// <summary>
    /// Updates the filtered models based on search text.
    /// </summary>
    private void UpdateFilteredModels()
    {
        FilteredModels.Clear();
        
        var searchLower = ModelSearchText?.ToLowerInvariant() ?? string.Empty;
        
        foreach (var model in AvailableModels)
        {
            if (string.IsNullOrWhiteSpace(searchLower) ||
                model.DisplayName.ToLowerInvariant().Contains(searchLower) ||
                model.Id.ToLowerInvariant().Contains(searchLower) ||
                model.ProviderEnum.ToString().ToLowerInvariant().Contains(searchLower))
            {
                FilteredModels.Add(model);
            }
        }
    }
    
    partial void OnModelSearchTextChanged(string value)
    {
        UpdateFilteredModels();
    }
    
    /// <summary>
    /// Initializes the provider keys collection with all available providers.
    /// </summary>
    private void InitializeProviderKeys()
    {
        var allProviders = ProviderApiKeyModel.GetAllProviders();
        foreach (var provider in allProviders)
        {
            ProviderKeys.Add(new ProviderApiKeyViewModel(provider));
        }
    }
    
    /// <summary>
    /// Loads settings from storage.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        var settings = await _conversationStore.LoadSettingsAsync();
        
        // Load provider keys
        foreach (var providerKey in ProviderKeys)
        {
            if (settings.ApiKeys.TryGetValue(providerKey.Provider, out var apiKey))
            {
                providerKey.SetApiKey(apiKey);
            }
            
            // Handle Azure special properties
            if (providerKey.IsAzure)
            {
                providerKey.AzureEndpoint = settings.AzureEndpoint ?? string.Empty;
                providerKey.AzureOrganization = settings.AzureOrganization ?? string.Empty;
            }
        }
        
        // Load custom models
        CustomModels.Clear();
        foreach (var customModel in settings.CustomModels)
        {
            CustomModels.Add(new ModelOption
            {
                Id = customModel.Id,
                DisplayName = customModel.DisplayName,
                ProviderEnum = customModel.Provider,
                ApiName = customModel.ApiName,
                IsCustom = true,
                MaxContextTokens = customModel.MaxContextTokens
            });
        }
        
        // Load other settings
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == settings.SelectedModelId) 
            ?? AvailableModels.FirstOrDefault();
        
        // Update filtered models
        UpdateFilteredModels();
        
        EnableStreaming = settings.EnableStreaming;
        MaxTokens = settings.MaxTokens;
        Temperature = settings.Temperature;
        SaveConversationHistory = settings.SaveConversationHistory;
        Theme = settings.Theme;
        
            // Refresh available models (includes discovered + custom)
            await RefreshAvailableModelsAsync();
            
            // Update filtered models
            UpdateFilteredModels();
        
        // If selected model is not in available models, try to find it
        if (SelectedModel == null && !string.IsNullOrEmpty(settings.SelectedModelId))
        {
            SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == settings.SelectedModelId);
        }
        
        HasChanges = false;
    }
    
    /// <summary>
    /// Refreshes the available models list by discovering from configured providers.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAvailableModelsAsync()
    {
        IsDiscoveringModels = true;
        DiscoveryStatus = "Discovering models...";
        
        try
        {
            var settings = ToModel();
            var discoveredModels = await _modelDiscoveryService.DiscoverAllModelsAsync(settings);
            
            // Clear and rebuild available models
            AvailableModels.Clear();
            
            // Add discovered models
            foreach (var model in discoveredModels)
            {
                AvailableModels.Add(model);
            }
            
            // Add custom models
            foreach (var customModel in CustomModels)
            {
                AvailableModels.Add(customModel);
            }
            
            // Add default models if no models found
            if (AvailableModels.Count == 0)
            {
                foreach (var defaultModel in ModelOption.GetDefaultModels())
                {
                    AvailableModels.Add(defaultModel);
                }
            }
            
            // Update filtered models
            UpdateFilteredModels();
            
            DiscoveryStatus = $"Found {discoveredModels.Count} models from configured providers.";
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Error discovering models: {ex.Message}";
        }
        finally
        {
            IsDiscoveringModels = false;
        }
    }
    
    /// <summary>
    /// Adds a custom model.
    /// </summary>
    [RelayCommand]
    public void AddCustomModel()
    {
        if (string.IsNullOrWhiteSpace(NewCustomModelId))
        {
            ValidationError = "Model ID is required.";
            return;
        }
        
        if (string.IsNullOrWhiteSpace(NewCustomModelDisplayName))
        {
            NewCustomModelDisplayName = NewCustomModelId;
        }
        
        // Check if model already exists
        if (CustomModels.Any(m => m.Id == NewCustomModelId && m.ProviderEnum == NewCustomModelProvider))
        {
            ValidationError = "A custom model with this ID and provider already exists.";
            return;
        }
        
        var customModel = new ModelOption
        {
            Id = NewCustomModelId,
            DisplayName = NewCustomModelDisplayName,
            ProviderEnum = NewCustomModelProvider,
            ApiName = NewCustomModelId,
            IsCustom = true,
            MaxContextTokens = NewCustomModelMaxTokens
        };
        
        CustomModels.Add(customModel);
        AvailableModels.Add(customModel);
        
        // Update filtered models
        UpdateFilteredModels();
        
        // Reset form
        NewCustomModelId = string.Empty;
        NewCustomModelDisplayName = string.Empty;
        NewCustomModelProvider = LLmProviders.OpenAi;
        NewCustomModelMaxTokens = 4096;
        ValidationError = null;
        HasChanges = true;
    }
    
    /// <summary>
    /// Removes a custom model.
    /// </summary>
    [RelayCommand]
    public void RemoveCustomModel(ModelOption? model)
    {
        if (model == null || !model.IsCustom)
            return;
        
        CustomModels.Remove(model);
        AvailableModels.Remove(model);
        
        // Update filtered models
        UpdateFilteredModels();
        
        // If this was the selected model, clear selection
        if (SelectedModel == model)
        {
            SelectedModel = AvailableModels.FirstOrDefault();
        }
        
        HasChanges = true;
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
        
        // Refresh models after saving
        await RefreshAvailableModelsAsync();
        
        HasChanges = false;
        SettingsSaved?.Invoke(settings);
    }
    
    /// <summary>
    /// Validates the current settings.
    /// </summary>
    public bool Validate()
    {
        ValidationError = null;
        
        // Validate at least one provider is configured
        bool hasConfiguredProvider = false;
        foreach (var providerKey in ProviderKeys)
        {
            var error = providerKey.Validate();
            if (error != null)
            {
                ValidationError = error;
                return false;
            }
            
            if (providerKey.IsConfigured)
            {
                hasConfiguredProvider = true;
            }
        }
        
        if (!hasConfiguredProvider)
        {
            ValidationError = "At least one provider API key must be configured.";
            return false;
        }
        
        if (SelectedModel == null)
        {
            ValidationError = "A model must be selected.";
            return false;
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
        var settings = new SettingsModel
        {
            SelectedModelId = SelectedModel?.Id ?? "gpt-4",
            EnableStreaming = EnableStreaming,
            MaxTokens = MaxTokens,
            Temperature = Temperature,
            SaveConversationHistory = SaveConversationHistory,
            Theme = Theme
        };
        
        // Build API keys dictionary
        foreach (var providerKey in ProviderKeys)
        {
            if (providerKey.IsConfigured)
            {
                settings.ApiKeys[providerKey.Provider] = providerKey.ApiKey;
                
                // Handle Azure special properties
                if (providerKey.IsAzure)
                {
                    settings.AzureEndpoint = providerKey.AzureEndpoint;
                    settings.AzureOrganization = providerKey.AzureOrganization;
                }
            }
        }
        
        // Build custom models list
        settings.CustomModels = CustomModels.Select(m => new CustomModelOption
        {
            Id = m.Id,
            DisplayName = m.DisplayName,
            Provider = m.ProviderEnum,
            ApiName = m.ApiName,
            MaxContextTokens = m.MaxContextTokens
        }).ToList();
        
        return settings;
    }
    
    // Property change handlers
    partial void OnSelectedModelChanged(ModelOption? value) => HasChanges = true;
    partial void OnEnableStreamingChanged(bool value) => HasChanges = true;
    partial void OnMaxTokensChanged(int value) => HasChanges = true;
    partial void OnTemperatureChanged(double value) => HasChanges = true;
    partial void OnSaveConversationHistoryChanged(bool value) => HasChanges = true;
    partial void OnThemeChanged(string value) => HasChanges = true;
    
    /// <summary>
    /// Handles provider key changes.
    /// </summary>
    public void OnProviderKeyChanged()
    {
        HasChanges = true;
    }
}
