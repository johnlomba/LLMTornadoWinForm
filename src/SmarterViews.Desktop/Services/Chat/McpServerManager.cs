using System.IO;
using System.Text.Json;
using LlmTornado.Mcp;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Services.Chat;

/// <summary>
/// Service for managing Model Context Protocol (MCP) server configurations and connections.
/// Provides functionality to configure, connect, and manage external tool servers that extend
/// the capabilities of LLM conversations with custom tools.
/// </summary>
/// <remarks>
/// <para>
/// MCP servers can be configured using two transport types:
/// <list type="bullet">
///   <item><description>HTTP/SSE: Connect to a remote HTTP server</description></item>
///   <item><description>Stdio: Launch a local process and communicate via standard I/O</description></item>
/// </list>
/// </para>
/// <para>
/// Configuration is persisted to a JSON file in the user's LocalApplicationData folder.
/// The format follows the MCP specification for server definitions.
/// </para>
/// </remarks>
/// <example>
/// Example mcp-servers.json configuration:
/// <code>
/// {
///   "servers": {
///     "filesystem": {
///       "command": "npx",
///       "args": ["-y", "@anthropic/mcp-server-filesystem"],
///       "type": "stdio"
///     },
///     "remote-api": {
///       "url": "http://localhost:8080",
///       "type": "http"
///     }
///   }
/// }
/// </code>
/// </example>
public class McpServerManager : IDisposable
{
    private readonly string _configFilePath;
    private readonly Dictionary<string, MCPServer> _servers = new();
    private readonly Dictionary<string, McpServerViewModel> _serverViewModels = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when a server's connection status changes.
    /// Parameters: server label, new status, optional error message.
    /// </summary>
    public event Action<string, McpServerStatus, string?>? ServerStatusChanged;
    
    /// <summary>
    /// Event raised when a server is added or removed from configuration.
    /// </summary>
    public event Action? ServersConfigurationChanged;

    /// <summary>
    /// Gets all configured server view models, including those not yet connected.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerViewModel> ServerViewModels => _serverViewModels;

    /// <summary>
    /// Gets all initialized and connected MCP servers.
    /// Only contains servers that have been successfully initialized.
    /// </summary>
    public IReadOnlyDictionary<string, MCPServer> Servers => _servers;
    
    /// <summary>
    /// Gets the total count of configured servers.
    /// </summary>
    public int ConfiguredServerCount => _serverViewModels.Count;
    
    /// <summary>
    /// Gets the count of currently connected servers.
    /// </summary>
    public int ConnectedServerCount => _servers.Count;
    
    /// <summary>
    /// Gets whether any servers are currently connected.
    /// </summary>
    public bool HasConnectedServers => _servers.Count > 0;
    
    /// <summary>
    /// Gets the path to the MCP configuration file.
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    public McpServerManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmarterViews");
        
        Directory.CreateDirectory(appDataPath);
        _configFilePath = Path.Combine(appDataPath, "mcp-servers.json");
    }

    /// <summary>
    /// Loads server configurations from the JSON configuration file.
    /// Creates an empty configuration file if none exists.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
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
            
            ServersConfigurationChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading MCP servers: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves current server configurations to the JSON configuration file.
    /// </summary>
    /// <param name="config">Optional pre-built config. If null, builds from current view models.</param>
    /// <returns>A task representing the async operation.</returns>
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
    /// <param name="serverLabel">The label identifying the server to initialize.</param>
    /// <returns>True if the server was initialized successfully, false otherwise.</returns>
    /// <remarks>
    /// This method will:
    /// <list type="number">
    ///   <item><description>Dispose any existing connection to this server</description></item>
    ///   <item><description>Create a new connection based on the server's transport type</description></item>
    ///   <item><description>Initialize the connection and discover available tools</description></item>
    ///   <item><description>Update the server's status and raise the StatusChanged event</description></item>
    /// </list>
    /// </remarks>
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
    /// Initializes all configured servers in parallel.
    /// </summary>
    /// <returns>A task that completes when all server initializations finish.</returns>
    /// <remarks>
    /// Individual server failures do not prevent other servers from initializing.
    /// Check each server's status after this method completes.
    /// </remarks>
    public async Task InitializeAllServersAsync()
    {
        var tasks = _serverViewModels.Keys.Select(InitializeServerAsync);
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the connection status of a specific server.
    /// </summary>
    /// <param name="serverLabel">The label identifying the server.</param>
    /// <returns>The current status, or NotInitialized if the server is not configured.</returns>
    public McpServerStatus GetServerStatus(string serverLabel)
    {
        return _serverViewModels.TryGetValue(serverLabel, out var viewModel)
            ? viewModel.Status
            : McpServerStatus.NotInitialized;
    }

    /// <summary>
    /// Gets all available MCP tools from all connected servers.
    /// These tools can be passed to the LLM runtime for use in conversations.
    /// </summary>
    /// <returns>A list of tools in LlmTornado format.</returns>
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
    /// Adds a new server configuration or updates an existing one.
    /// The server is not automatically connected; call InitializeServerAsync after adding.
    /// </summary>
    /// <param name="label">Unique label for the server.</param>
    /// <param name="definition">The server definition with connection details.</param>
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
        ServersConfigurationChanged?.Invoke();
    }

    /// <summary>
    /// Removes a server configuration and disconnects it if connected.
    /// </summary>
    /// <param name="label">The label of the server to remove.</param>
    public void RemoveServer(string label)
    {
        if (_servers.TryGetValue(label, out var server))
        {
            _servers.Remove(label);
        }

        _serverViewModels.Remove(label);
        ServersConfigurationChanged?.Invoke();
    }

    /// <summary>
    /// Gets a server view model by its label.
    /// </summary>
    /// <param name="label">The server label.</param>
    /// <returns>The view model if found, null otherwise.</returns>
    public McpServerViewModel? GetServerViewModel(string label)
    {
        return _serverViewModels.TryGetValue(label, out var viewModel) ? viewModel : null;
    }

    /// <summary>
    /// Gets a tool by name from any connected server.
    /// </summary>
    /// <param name="toolName">The name of the tool to find.</param>
    /// <returns>The server label containing the tool, or null if not found.</returns>
    public string? FindToolServer(string toolName)
    {
        foreach (var (label, server) in _servers)
        {
            if (server.AllTools.Any(t => t.Name == toolName))
            {
                return label;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Gets a summary of all connected servers and their tool counts.
    /// Useful for debugging and status displays.
    /// </summary>
    /// <returns>A dictionary mapping server labels to their tool counts.</returns>
    public Dictionary<string, int> GetServerToolCounts()
    {
        return _servers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.AllTools.Count);
    }
    
    /// <summary>
    /// Disconnects a specific server while keeping its configuration.
    /// </summary>
    /// <param name="serverLabel">The label of the server to disconnect.</param>
    public void DisconnectServer(string serverLabel)
    {
        if (_servers.TryGetValue(serverLabel, out var server))
        {
            _servers.Remove(serverLabel);
        }
        
        if (_serverViewModels.TryGetValue(serverLabel, out var viewModel))
        {
            viewModel.Status = McpServerStatus.Disconnected;
            viewModel.Tools.Clear();
        }
        
        ServerStatusChanged?.Invoke(serverLabel, McpServerStatus.Disconnected, null);
    }
    
    /// <summary>
    /// Disconnects all servers while keeping their configurations.
    /// </summary>
    public void DisconnectAllServers()
    {
        foreach (var label in _servers.Keys.ToList())
        {
            DisconnectServer(label);
        }
    }

    /// <summary>
    /// Disposes all server connections and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _servers.Clear();
        GC.SuppressFinalize(this);
    }
}
