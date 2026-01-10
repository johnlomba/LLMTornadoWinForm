using System.Text.Json.Serialization;

namespace SmarterViews.Desktop.Models.Chat;

/// <summary>
/// Root configuration for MCP servers matching the standard MCP JSON format.
/// </summary>
public class McpServerConfig
{
    /// <summary>
    /// Dictionary of server configurations keyed by server label.
    /// </summary>
    [JsonPropertyName("servers")]
    public Dictionary<string, McpServerDefinition> Servers { get; set; } = new();
}

/// <summary>
/// Individual MCP server configuration definition.
/// </summary>
public class McpServerDefinition
{
    /// <summary>
    /// Transport type: "stdio" or "http"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "stdio";

    /// <summary>
    /// Command to execute for stdio transport (e.g., "npx", "node", "python").
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    /// <summary>
    /// Arguments to pass to the command for stdio transport.
    /// </summary>
    [JsonPropertyName("args")]
    public string[]? Args { get; set; }

    /// <summary>
    /// Working directory for stdio transport.
    /// </summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Environment variables for stdio transport.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>
    /// URL for HTTP transport.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// HTTP headers for HTTP transport (e.g., Authorization).
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// List of allowed tool names (optional filter).
    /// </summary>
    [JsonPropertyName("allowedTools")]
    public string[]? AllowedTools { get; set; }
}

/// <summary>
/// Status of an MCP server connection.
/// </summary>
public enum McpServerStatus
{
    /// <summary>
    /// Server not initialized.
    /// </summary>
    NotInitialized,

    /// <summary>
    /// Server is connected and ready.
    /// </summary>
    Connected,

    /// <summary>
    /// Server connection failed.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Server connection error.
    /// </summary>
    Error
}

/// <summary>
/// View model for displaying MCP server information in the UI.
/// </summary>
public class McpServerViewModel
{
    /// <summary>
    /// Server label/name.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Server configuration definition.
    /// </summary>
    public McpServerDefinition Definition { get; set; } = new();

    /// <summary>
    /// Current connection status.
    /// </summary>
    public McpServerStatus Status { get; set; } = McpServerStatus.NotInitialized;

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of available tools from this server.
    /// </summary>
    public List<McpToolViewModel> Tools { get; set; } = new();

    /// <summary>
    /// Number of tools available.
    /// </summary>
    public int ToolCount => Tools.Count;
}

/// <summary>
/// View model for displaying MCP tool information in the UI.
/// </summary>
public class McpToolViewModel
{
    /// <summary>
    /// Tool name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tool description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Server label this tool belongs to.
    /// </summary>
    public string ServerLabel { get; set; } = string.Empty;
}
