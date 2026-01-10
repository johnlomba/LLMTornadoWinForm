using System.Windows;
using System.Windows.Controls;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews.Views;

/// <summary>
/// Interaction logic for SettingsDialog.xaml
/// </summary>
public partial class SettingsDialog : UserControl
{
    /// <summary>
    /// Event raised when close is requested.
    /// </summary>
    public event EventHandler? CloseRequested;
    
    public SettingsDialog()
    {
        InitializeComponent();
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void ProviderKey_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.OnProviderKeyChanged();
        }
    }
    
    private void ModelComboBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox && DataContext is SettingsViewModel viewModel)
        {
            // Only update search text if the text doesn't match the selected item's display name
            // This prevents clearing the search when the selected item's display name is shown
            if (comboBox.SelectedItem == null || 
                comboBox.Text != (comboBox.SelectedItem as LlmTornado.WpfViews.Models.ModelOption)?.DisplayName)
            {
                viewModel.ModelSearchText = comboBox.Text ?? string.Empty;
            }
        }
    }
    
    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox && DataContext is SettingsViewModel viewModel)
        {
            // Clear search text when a model is selected and update the text to show the display name
            if (comboBox.SelectedItem is LlmTornado.WpfViews.Models.ModelOption selectedModel)
            {
                viewModel.ModelSearchText = string.Empty;
                comboBox.Text = selectedModel.DisplayName;
            }
        }
    }
}

