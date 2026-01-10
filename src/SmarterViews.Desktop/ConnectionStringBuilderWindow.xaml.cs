using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmarterViews.Desktop.Services;

namespace SmarterViews.Desktop;

/// <summary>
/// Interaction logic for ConnectionStringBuilderWindow.xaml
/// </summary>
public partial class ConnectionStringBuilderWindow : Window
{
    private readonly ConnectionStringBuilder _builder;
    private readonly ConnectionTestService _testService;
    private readonly string _initialDatabaseType;

    public string? ResultConnectionString { get; private set; }
    public string? ResultDatabaseType { get; private set; }

    public ConnectionStringBuilderWindow(string databaseType = "SqlServer", string? existingConnectionString = null)
    {
        InitializeComponent();
        
        _builder = new ConnectionStringBuilder();
        _testService = new ConnectionTestService();
        _initialDatabaseType = databaseType;

        // Populate database types
        foreach (var type in DatabaseTypes.Types)
        {
            DatabaseTypeComboBox.Items.Add(type);
        }

        DatabaseTypeComboBox.SelectedItem = databaseType;

        // Parse existing connection string if provided
        if (!string.IsNullOrWhiteSpace(existingConnectionString))
        {
            _builder.Parse(existingConnectionString, databaseType);
            PopulateFieldsFromBuilder();
        }

        UpdatePreview();
    }

    private void PopulateFieldsFromBuilder()
    {
        ServerTextBox.Text = _builder.Server;
        DatabaseTextBox.Text = _builder.Database;
        UsernameTextBox.Text = _builder.Username;
        PasswordBox.Password = _builder.Password;
        FilePathTextBox.Text = _builder.FilePath;
        IntegratedSecurityCheckBox.IsChecked = _builder.IntegratedSecurity;
        
        if (_builder.Port > 0)
            PortTextBox.Text = _builder.Port.ToString();
    }

    private void DatabaseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DatabaseTypeComboBox.SelectedItem == null) return;

        var selectedType = DatabaseTypeComboBox.SelectedItem.ToString();

        // Show/hide appropriate panels
        bool isSQLite = selectedType == "SQLite";
        ServerBasedPanel.Visibility = isSQLite ? Visibility.Collapsed : Visibility.Visible;
        SQLitePanel.Visibility = isSQLite ? Visibility.Visible : Visibility.Collapsed;

        // Show/hide integrated security (SQL Server only)
        bool isSqlServer = selectedType == "SqlServer";
        IntegratedSecurityCheckBox.Visibility = isSqlServer ? Visibility.Visible : Visibility.Collapsed;

        // Set default port hint
        if (!isSQLite && string.IsNullOrWhiteSpace(PortTextBox.Text))
        {
            var defaultPort = ConnectionStringBuilder.GetDefaultPort(selectedType!);
            if (defaultPort > 0)
                PortTextBox.Text = defaultPort.ToString();
        }

        UpdatePreview();
    }

    private void IntegratedSecurity_Changed(object sender, RoutedEventArgs e)
    {
        bool integratedSecurity = IntegratedSecurityCheckBox.IsChecked == true;
        
        // Disable username/password when using integrated security
        UsernameTextBox.IsEnabled = !integratedSecurity;
        PasswordBox.IsEnabled = !integratedSecurity;
        UsernameLabel.IsEnabled = !integratedSecurity;
        PasswordLabel.IsEnabled = !integratedSecurity;

        UpdatePreview();
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQLite Database (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|All Files (*.*)|*.*",
            CheckFileExists = false,
            Title = "Select SQLite Database File"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePathTextBox.Text = dialog.FileName;
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        try
        {
            var selectedType = DatabaseTypeComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selectedType))
            {
                PreviewTextBox.Text = "";
                return;
            }

            // Update builder from UI
            _builder.Server = ServerTextBox.Text;
            _builder.Database = DatabaseTextBox.Text;
            _builder.Username = UsernameTextBox.Text;
            _builder.Password = PasswordBox.Password;
            _builder.FilePath = FilePathTextBox.Text;
            _builder.IntegratedSecurity = IntegratedSecurityCheckBox.IsChecked == true;

            if (int.TryParse(PortTextBox.Text, out int port))
                _builder.Port = port;
            else
                _builder.Port = 0;

            // Build and display
            var connectionString = _builder.Build(selectedType);
            PreviewTextBox.Text = connectionString;
        }
        catch (Exception ex)
        {
            PreviewTextBox.Text = $"Error: {ex.Message}";
        }
    }

    private void ServerTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void DatabaseTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) => UpdatePreview();
    private void PortTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void FilePathTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedType = DatabaseTypeComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selectedType))
            {
                MessageBox.Show("Please select a database type.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var connectionString = _builder.Build(selectedType);
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Please fill in the required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show testing message
            var testButton = (Button)sender;
            var originalContent = testButton.Content;
            testButton.Content = "Testing...";
            testButton.IsEnabled = false;

            try
            {
                // Test the actual connection
                var result = await _testService.TestConnectionAsync(connectionString, selectedType);
                
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
            finally
            {
                testButton.Content = originalContent;
                testButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UseConnectionString_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedType = DatabaseTypeComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selectedType))
            {
                MessageBox.Show("Please select a database type.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var connectionString = _builder.Build(selectedType);
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Please fill in the required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultConnectionString = connectionString;
            ResultDatabaseType = selectedType;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error building connection string: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
