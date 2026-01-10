using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LlmTornado.Code;
using SmarterViews.Desktop.Models.Chat;
using SmarterViews.Desktop.Services.Chat;

namespace SmarterViews.Desktop.Views;

/// <summary>
/// Interaction logic for AiSettingsWindow.xaml
/// </summary>
public partial class AiSettingsWindow : Window
{
    private readonly ConversationStore _conversationStore;
    private SettingsModel _settings;
    private bool _isLoading = true;

    public AiSettingsWindow(SettingsModel settings, ConversationStore conversationStore)
    {
        InitializeComponent();
        _conversationStore = conversationStore;
        _settings = settings;
        
        LoadSettings();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        // Load API keys (show masked if present)
        if (_settings.ApiKeys.TryGetValue(LLmProviders.OpenAi, out var openAiKey) && !string.IsNullOrWhiteSpace(openAiKey))
        {
            OpenAiApiKeyBox.Password = openAiKey;
        }
        
        if (_settings.ApiKeys.TryGetValue(LLmProviders.Anthropic, out var anthropicKey) && !string.IsNullOrWhiteSpace(anthropicKey))
        {
            AnthropicApiKeyBox.Password = anthropicKey;
        }
        
        if (_settings.ApiKeys.TryGetValue(LLmProviders.Google, out var googleKey) && !string.IsNullOrWhiteSpace(googleKey))
        {
            GoogleApiKeyBox.Password = googleKey;
        }
        
        // Load other settings
        TemperatureSlider.Value = _settings.Temperature;
        MaxTokensBox.Text = _settings.MaxTokens.ToString();
        
        UpdateStatus();
    }

    private void ApiKey_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        
        if (sender is PasswordBox passwordBox && passwordBox.Tag is string providerName)
        {
            if (Enum.TryParse<LLmProviders>(providerName, out var provider))
            {
                _settings.ApiKeys[provider] = passwordBox.Password;
                SaveSettings();
            }
        }
    }

    private void Temperature_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        
        _settings.Temperature = e.NewValue;
        SaveSettings();
    }

    private void MaxTokens_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        
        if (int.TryParse(MaxTokensBox.Text, out int maxTokens) && maxTokens > 0)
        {
            _settings.MaxTokens = maxTokens;
            SaveSettings();
        }
    }

    private async void SaveSettings()
    {
        try
        {
            await _conversationStore.SaveSettingsAsync(_settings);
            UpdateStatus("Settings saved", true);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}", false);
        }
    }

    private void UpdateStatus(string? message = null, bool success = true)
    {
        var configuredCount = _settings.ApiKeys.Count(kvp => !string.IsNullOrWhiteSpace(kvp.Value));
        
        if (message != null)
        {
            StatusText.Text = message;
        }
        else
        {
            StatusText.Text = configuredCount > 0 
                ? $"{configuredCount} provider(s) configured" 
                : "No API keys configured";
        }
        
        StatusIndicator.Fill = success && configuredCount > 0 
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)) 
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Gets the updated settings after the dialog is closed.
    /// </summary>
    public SettingsModel GetSettings() => _settings;
}
