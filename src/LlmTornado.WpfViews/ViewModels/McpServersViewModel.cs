using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado.WpfViews.Models;
using LlmTornado.WpfViews.Services;

namespace LlmTornado.WpfViews.ViewModels;

/// <summary>
/// ViewModel for the MCP Servers management dialog.
/// </summary>
public partial class McpServersViewModel : ObservableObject
{
    private readonly McpServerManager _serverManager;
    private McpServerViewModel? _selectedServer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _newServerLabel = string.Empty;

    [ObservableProperty]
    private McpServerDefinition _editingServer = new();

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isAddingNew;

    /// <summary>
    /// Collection of server view models.
    /// </summary>
    public ObservableCollection<McpServerViewModel> Servers { get; } = [];

    /// <summary>
    /// Selected server for editing/viewing.
    /// </summary>
    public McpServerViewModel? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                if (value != null)
                {
                    EditingServer = new McpServerDefinition
                    {
                        Type = value.Definition.Type,
                        Command = value.Definition.Command,
                        Args = value.Definition.Args?.ToArray(),
                        WorkingDirectory = value.Definition.WorkingDirectory,
                        Env = value.Definition.Env != null ? new Dictionary<string, string>(value.Definition.Env) : null,
                        Url = value.Definition.Url,
                        Headers = value.Definition.Headers != null ? new Dictionary<string, string>(value.Definition.Headers) : null,
                        AllowedTools = value.Definition.AllowedTools?.ToArray()
                    };
                    NewServerLabel = value.Label;
                }
                else
                {
                    EditingServer = new McpServerDefinition();
                    NewServerLabel = string.Empty;
                }
            }
        }
    }

    public McpServersViewModel(McpServerManager serverManager)
    {
        _serverManager = serverManager;
        _serverManager.ServerStatusChanged += OnServerStatusChanged;
    }

    /// <summary>
    /// Loads servers from configuration.
    /// </summary>
    [RelayCommand]
    public async Task LoadServersAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _serverManager.LoadServersAsync();
            RefreshServersList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading servers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Initializes all servers.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAllServersAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _serverManager.InitializeAllServersAsync();
            RefreshServersList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error initializing servers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Initializes a specific server.
    /// </summary>
    [RelayCommand]
    public async Task InitializeServerAsync(string? serverLabel)
    {
        if (string.IsNullOrEmpty(serverLabel))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await _serverManager.InitializeServerAsync(serverLabel);
            RefreshServersList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error initializing server: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Adds a new server.
    /// </summary>
    [RelayCommand]
    public void AddServer()
    {
        IsEditing = false;
        IsAddingNew = true;
        SelectedServer = null;
        EditingServer = new McpServerDefinition { Type = "stdio" };
        NewServerLabel = string.Empty;
        ErrorMessage = null;
    }

    /// <summary>
    /// Saves the current server (new or edited).
    /// </summary>
    [RelayCommand]
    public async Task SaveServerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewServerLabel))
        {
            ErrorMessage = "Server label is required.";
            return;
        }

        ErrorMessage = null;

        try
        {
            _serverManager.AddOrUpdateServer(NewServerLabel, EditingServer);
            await _serverManager.SaveServersAsync();
            RefreshServersList();
            
            // Select the saved server
            SelectedServer = Servers.FirstOrDefault(s => s.Label == NewServerLabel);
            IsEditing = false;
            IsAddingNew = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving server: {ex.Message}";
        }
    }

    /// <summary>
    /// Starts editing the selected server.
    /// </summary>
    [RelayCommand]
    public void EditServer()
    {
        if (SelectedServer != null)
        {
            IsEditing = true;
        }
    }

    /// <summary>
    /// Cancels editing.
    /// </summary>
    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        IsAddingNew = false;
        if (SelectedServer != null)
        {
            EditingServer = new McpServerDefinition
            {
                Type = SelectedServer.Definition.Type,
                Command = SelectedServer.Definition.Command,
                Args = SelectedServer.Definition.Args?.ToArray(),
                WorkingDirectory = SelectedServer.Definition.WorkingDirectory,
                Env = SelectedServer.Definition.Env != null ? new Dictionary<string, string>(SelectedServer.Definition.Env) : null,
                Url = SelectedServer.Definition.Url,
                Headers = SelectedServer.Definition.Headers != null ? new Dictionary<string, string>(SelectedServer.Definition.Headers) : null,
                AllowedTools = SelectedServer.Definition.AllowedTools?.ToArray()
            };
            NewServerLabel = SelectedServer.Label;
        }
        else
        {
            EditingServer = new McpServerDefinition();
            NewServerLabel = string.Empty;
        }
    }

    /// <summary>
    /// Removes the selected server.
    /// </summary>
    [RelayCommand]
    public async Task RemoveServerAsync()
    {
        if (SelectedServer == null)
            return;

        var label = SelectedServer.Label;
        _serverManager.RemoveServer(label);
        await _serverManager.SaveServersAsync();
        RefreshServersList();
        SelectedServer = null;
    }

    /// <summary>
    /// Tests connection to the selected server.
    /// </summary>
    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        if (SelectedServer == null)
            return;

        await InitializeServerAsync(SelectedServer.Label);
    }

    /// <summary>
    /// Refreshes the servers list from the manager.
    /// </summary>
    private void RefreshServersList()
    {
        var currentSelection = SelectedServer?.Label;
        
        Servers.Clear();
        foreach (var (label, viewModel) in _serverManager.ServerViewModels)
        {
            Servers.Add(viewModel);
        }

        // Restore selection
        if (!string.IsNullOrEmpty(currentSelection))
        {
            SelectedServer = Servers.FirstOrDefault(s => s.Label == currentSelection);
        }
    }

    /// <summary>
    /// Handles server status changes from the manager.
    /// </summary>
    private void OnServerStatusChanged(string serverLabel, McpServerStatus status, string? errorMessage)
    {
        var server = Servers.FirstOrDefault(s => s.Label == serverLabel);
        if (server != null)
        {
            server.Status = status;
            server.ErrorMessage = errorMessage;
        }
    }
}

