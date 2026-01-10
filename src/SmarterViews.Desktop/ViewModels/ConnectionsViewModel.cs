using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmarterViews.Desktop.Models;
using SmarterViews.Desktop.Services;

namespace SmarterViews.Desktop.ViewModels;

/// <summary>
/// ViewModel for managing database connections
/// </summary>
public partial class ConnectionsViewModel : ViewModelBase
{
    private readonly ConnectionStorageService _storageService;

    [ObservableProperty]
    private ObservableCollection<DatabaseConnection> connections = new();

    [ObservableProperty]
    private DatabaseConnection? selectedConnection;

    [ObservableProperty]
    private string connectionName = string.Empty;

    [ObservableProperty]
    private string connectionString = string.Empty;

    [ObservableProperty]
    private string databaseType = "SqlServer";

    [ObservableProperty]
    private bool isDefault;

    public ConnectionsViewModel() : this(null)
    {
    }

    public ConnectionsViewModel(ConnectionStorageService? storageService = null)
    {
        _storageService = storageService ?? new ConnectionStorageService();
    }

    /// <summary>
    /// Loads all connections from storage
    /// </summary>
    [RelayCommand]
    public async Task LoadConnections()
    {
        try
        {
            IsLoading = true;
            ClearError();
            var loadedConnections = await _storageService.LoadAllConnectionsAsync();
            Connections = loadedConnections;
        }
        catch (Exception ex)
        {
            SetError($"Failed to load connections: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the current connection
    /// </summary>
    [RelayCommand]
    public async Task SaveConnection()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionName))
            {
                SetError("Connection name is required");
                return;
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                SetError("Connection string is required");
                return;
            }

            IsLoading = true;
            ClearError();

            var connection = new DatabaseConnection
            {
                Name = ConnectionName,
                ConnectionString = ConnectionString,
                DatabaseType = DatabaseType,
                IsDefault = IsDefault
            };

            if (IsDefault)
            {
                // Unset default on all other connections
                foreach (var conn in Connections)
                {
                    conn.IsDefault = false;
                }
            }

            await _storageService.SaveConnectionAsync(connection);

            if (!Connections.Any(c => c.Id == connection.Id))
            {
                Connections.Add(connection);
            }
            else
            {
                var index = Connections.IndexOf(Connections.First(c => c.Id == connection.Id));
                Connections[index] = connection;
            }

            ClearForm();
        }
        catch (Exception ex)
        {
            SetError($"Failed to save connection: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deletes the selected connection
    /// </summary>
    [RelayCommand]
    public async Task DeleteConnection()
    {
        if (SelectedConnection == null)
        {
            SetError("No connection selected");
            return;
        }

        try
        {
            IsLoading = true;
            ClearError();
            await _storageService.DeleteConnectionAsync(SelectedConnection.Id);
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            ClearForm();
        }
        catch (Exception ex)
        {
            SetError($"Failed to delete connection: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Edits the selected connection
    /// </summary>
    [RelayCommand]
    public void EditConnection()
    {
        if (SelectedConnection == null)
        {
            SetError("No connection selected");
            return;
        }

        ConnectionName = SelectedConnection.Name;
        ConnectionString = SelectedConnection.ConnectionString;
        DatabaseType = SelectedConnection.DatabaseType;
        IsDefault = SelectedConnection.IsDefault;
    }

    /// <summary>
    /// Clears the form fields
    /// </summary>
    [RelayCommand]
    public void ClearForm()
    {
        ConnectionName = string.Empty;
        ConnectionString = string.Empty;
        DatabaseType = "SqlServer";
        IsDefault = false;
    }
}
