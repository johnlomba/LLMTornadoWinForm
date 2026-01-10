using System.Windows;
using System.Windows.Controls;
using LlmTornado.WpfViews.ViewModels;

namespace LlmTornado.WpfViews.Views;

/// <summary>
/// Interaction logic for McpServersDialog.xaml
/// </summary>
public partial class McpServersDialog : UserControl
{
    public event EventHandler? CloseRequested;

    public McpServersDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is McpServersViewModel viewModel)
        {
            await viewModel.LoadServersAsync();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ArgsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is McpServersViewModel viewModel && sender is TextBox textBox)
        {
            var lines = textBox.Text.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            
            viewModel.EditingServer.Args = lines;
        }
    }

    private void AllowedToolsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is McpServersViewModel viewModel && sender is TextBox textBox)
        {
            var tools = textBox.Text.Split(new string[] { ",", ";", Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            
            viewModel.EditingServer.AllowedTools = tools;
        }
    }
}

