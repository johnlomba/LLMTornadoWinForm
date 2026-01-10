using System.Windows;
using System.Windows.Controls;
using SmarterViews.Desktop.ViewModels;

namespace SmarterViews.Desktop.Views;

/// <summary>
/// Interaction logic for McpServersWindow.xaml
/// </summary>
public partial class McpServersWindow : Window
{
    private readonly McpServersViewModel _viewModel;

    public McpServersWindow(McpServersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadServersAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ArgsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var lines = textBox.Text.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            
            _viewModel.EditingServer.Args = lines;
        }
    }

    private void AllowedToolsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var tools = textBox.Text.Split(new string[] { ",", ";", Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            
            _viewModel.EditingServer.AllowedTools = tools;
        }
    }
}
