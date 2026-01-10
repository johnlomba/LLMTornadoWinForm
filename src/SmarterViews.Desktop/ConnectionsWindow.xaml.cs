using System.Windows;
using SmarterViews.Desktop.ViewModels;
using SmarterViews.Desktop.Services;

namespace SmarterViews.Desktop;

/// <summary>
/// Interaction logic for ConnectionsWindow.xaml
/// </summary>
public partial class ConnectionsWindow : Window
{
    private readonly ConnectionTestService _testService;

    public ConnectionsWindow()
    {
        InitializeComponent();
        _testService = new ConnectionTestService();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionsViewModel viewModel)
        {
            viewModel.LoadConnectionsCommand.Execute(null);
        }
    }

    private void BuildConnectionString_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionsViewModel viewModel)
        {
            var builderWindow = new ConnectionStringBuilderWindow(
                viewModel.DatabaseType,
                viewModel.ConnectionString)
            {
                Owner = this
            };

            if (builderWindow.ShowDialog() == true)
            {
                viewModel.ConnectionString = builderWindow.ResultConnectionString ?? string.Empty;
                
                // Update database type if it was changed
                if (!string.IsNullOrWhiteSpace(builderWindow.ResultDatabaseType))
                {
                    viewModel.DatabaseType = builderWindow.ResultDatabaseType;
                }
            }
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionsViewModel viewModel)
            return;

        if (string.IsNullOrWhiteSpace(viewModel.ConnectionString))
        {
            MessageBox.Show("Please enter a connection string first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var button = (System.Windows.Controls.Button)sender;
        var originalContent = button.Content;
        button.Content = "Testing...";
        button.IsEnabled = false;

        try
        {
            var result = await _testService.TestConnectionAsync(
                viewModel.ConnectionString,
                viewModel.DatabaseType);

            if (result.Success)
            {
                MessageBox.Show(
                    result.Message,
                    "Connection Test Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                var errorMessage = result.Message;
                if (!string.IsNullOrWhiteSpace(result.ErrorDetails))
                {
                    errorMessage += $"\n\n{result.ErrorDetails}";
                }

                MessageBox.Show(
                    errorMessage,
                    "Connection Test Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error testing connection: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            button.Content = originalContent;
            button.IsEnabled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
