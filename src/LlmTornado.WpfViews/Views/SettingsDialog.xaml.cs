using System.Windows;
using System.Windows.Controls;

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
}

