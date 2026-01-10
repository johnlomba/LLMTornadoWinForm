using System.IO;
using System.Text.Json;
using LlmTornado.Mcp;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for managing MCP server configurations and connections.
/// </summary>
public class McpServerManager
{
    private readonly string _configFilePath;
    private readonly Dictionary<string, MCPServer> _servers = new();
    private readonly Dictionary<string, McpServerViewModel> _serverViewModels = new();

    /// <summary>
    /// Event raised when a server's status changes.
    /// </summary>
    public event Action<string, McpServerStatus, string?>? ServerStatusChanged;

    /// <summary>
    /// Gets all configured server view models.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerViewModel> ServerViewModels => _serverViewModels;

    /// <summary>
    /// Gets all initialized MCP servers.
    /// </summary>
    public IReadOnlyDictionary<string, MCPServer> Servers => _servers;

    public McpServerManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmarterViews");
        
        Directory.CreateDirectory(appDataPath);
        _configFilePath = Path.Combine(appDataPath, "mcp-servers.json");
    }

    /// <summary>
    /// Loads server configurations from the JSON file.
    /// </summary>
    public async Task LoadServersAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            // Create empty config file
            var emptyConfig = new McpServerConfig();
            await SaveServersAsync(emptyConfig);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<McpServerConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new McpServerConfig();

            // Create view models for all configured servers
            foreach (var (label, definition) in config.Servers)
            {
                _serverViewModels[label] = new McpServerViewModel
                {
                    Label = label,
                    Definition = definition,
                    Status = McpServerStatus.NotInitialized
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading MCP servers: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves server configurations to the JSON file.
    /// </summary>
    public async Task SaveServersAsync(McpServerConfig? config = null)
    {
        config ??= new McpServerConfig();
        
        // Build config from view models
        foreach (var (label, viewModel) in _serverViewModels)
        {
            config.Servers[label] = viewModel.Definition;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(_configFilePath, json);
    }

    /// <summary>
    /// Initializes a specific MCP server and loads its tools.
    /// </summary>
    public async Task<bool> InitializeServerAsync(string serverLabel)
    {
        if (!_serverViewModels.TryGetValue(serverLabel, out var viewModel))
        {
            return false;
        }

        try
        {
            // Dispose existing server if any
            if (_servers.TryGetValue(serverLabel, out var existingServer))
            {
                _servers.Remove(serverLabel);
            }

            var definition = viewModel.Definition;
            MCPServer mcpServer;

            if (definition.Type == "http" && !string.IsNullOrEmpty(definition.Url))
            {
                // HTTP transport
                mcpServer = new MCPServer(
                    serverLabel,
                    definition.Url,
                    allowedTools: definition.AllowedTools,
                    additionalConnectionHeaders: definition.Headers
                );
            }
            else
            {
                // Stdio transport
                mcpServer = new MCPServer(
                    serverLabel,
                    definition.Command ?? string.Empty,
                    definition.Args,
                    workingDirectory: definition.WorkingDirectory ?? string.Empty,
                    environmentVariables: definition.Env ?? new Dictionary<string, string>(),
                    allowedTools: definition.AllowedTools
                );
            }

            // Initialize the server
            await mcpServer.InitializeAsync();

            // Check if initialization was successful
            if (mcpServer.McpClient != null && mcpServer.Tools.Count > 0)
            {
                _servers[serverLabel] = mcpServer;
                viewModel.Status = McpServerStatus.Connected;
                viewModel.ErrorMessage = null;

                // Update tools list
                viewModel.Tools = mcpServer.AllTools.Select(tool => new McpToolViewModel
                {
                    Name = tool.Name,
                    Description = tool.Description ?? string.Empty,
                    ServerLabel = serverLabel
                }).ToList();

                ServerStatusChanged?.Invoke(serverLabel, McpServerStatus.Connected, null);
                return true;
            }
            else
            {
                viewModel.Status = McpServerStatus.Disconnected;
                viewModel.ErrorMessage = "Failed to connect or no tools available";
                ServerStatusChanged?.Invoke(serverLabel, McpServerStatus.Disconnected, viewModel.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            viewModel.Status = McpServerStatus.Error;
            viewModel.ErrorMessage = ex.Message;
            ServerStatusChanged?.Invoke(serverLabel, McpServerStatus.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Initializes all configured servers.
    /// </summary>
    public async Task InitializeAllServersAsync()
    {
        var tasks = _serverViewModels.Keys.Select(InitializeServerAsync);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the connection status of a server.
    /// </summary>
    public McpServerStatus GetServerStatus(string serverLabel)
    {
        return _serverViewModels.TryGetValue(serverLabel, out var viewModel)
            ? viewModel.Status
            : McpServerStatus.NotInitialized;
    }

    /// <summary>
    /// Gets all available MCP tools from all connected servers.
    /// </summary>
    public List<LlmTornado.Common.Tool> GetAllTools()
    {
        var tools = new List<LlmTornado.Common.Tool>();
        
        foreach (var server in _servers.Values)
        {
            tools.AddRange(server.AllowedTornadoTools);
        }

        return tools;
    }

    /// <summary>
    /// Adds or updates a server configuration.
    /// </summary>
    public void AddOrUpdateServer(string label, McpServerDefinition definition)
    {
        if (_serverViewModels.TryGetValue(label, out var existing))
        {
            existing.Definition = definition;
            existing.Status = McpServerStatus.NotInitialized;
            existing.ErrorMessage = null;
            existing.Tools.Clear();
        }
        else
        {
            _serverViewModels[label] = new McpServerViewModel
            {
                Label = label,
                Definition = definition,
                Status = McpServerStatus.NotInitialized
            };
        }

        // Dispose existing server connection if any
        if (_servers.TryGetValue(label, out var server))
        {
            _servers.Remove(label);
        }
    }

    /// <summary>
    /// Removes a server configuration.
    /// </summary>
    public void RemoveServer(string label)
    {
        if (_servers.TryGetValue(label, out var server))
        {
            _servers.Remove(label);
        }

        _serverViewModels.Remove(label);
    }

    /// <summary>
    /// Gets a server view model by label.
    /// </summary>
    public McpServerViewModel? GetServerViewModel(string label)
    {
        return _serverViewModels.TryGetValue(label, out var viewModel) ? viewModel : null;
    }

    /// <summary>
    /// Disposes all server connections.
    /// </summary>
    public void Dispose()
    {
        _servers.Clear();
    }
}
