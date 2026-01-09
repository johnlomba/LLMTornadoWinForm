namespace LlmTornado.Mcp;

/// <summary>
/// Configuration options for Microsoft SQL Server MCP connection.
/// </summary>
public class MsSqlOptions
{
    /// <summary>
    /// ODBC driver name. Default: "SQL Server"
    /// </summary>
    public string Driver { get; set; } = "SQL Server";
    
    /// <summary>
    /// Database server hostname or IP address.
    /// </summary>
    public string Host { get; set; } = "localhost";
    
    /// <summary>
    /// Database name to connect to.
    /// </summary>
    public string Database { get; set; } = "";
    
    /// <summary>
    /// SQL Server username.
    /// </summary>
    public string User { get; set; } = "";
    
    /// <summary>
    /// SQL Server password.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Work environment preset (VISUAL01/SAN database).
    /// Password is loaded from MSSQL_PASSWORD environment variable.
    /// </summary>
    public static MsSqlOptions Work => new()
    {
        Driver = "SQL Server",
        Host = "VISUAL01",
        Database = "SAN",
        User = "CURSOR_AI",
        Password = Environment.GetEnvironmentVariable("MSSQL_PASSWORD") ?? ""
    };

    /// <summary>
    /// Creates options from environment variables.
    /// Reads: MSSQL_DRIVER, MSSQL_HOST, MSSQL_DATABASE, MSSQL_USER, MSSQL_PASSWORD
    /// </summary>
    public static MsSqlOptions FromEnvironment() => new()
    {
        Driver = Environment.GetEnvironmentVariable("MSSQL_DRIVER") ?? "SQL Server",
        Host = Environment.GetEnvironmentVariable("MSSQL_HOST") ?? "localhost",
        Database = Environment.GetEnvironmentVariable("MSSQL_DATABASE") ?? "",
        User = Environment.GetEnvironmentVariable("MSSQL_USER") ?? "",
        Password = Environment.GetEnvironmentVariable("MSSQL_PASSWORD") ?? ""
    };
}

