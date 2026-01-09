namespace LlmTornado.Mcp;

/// <summary>
/// Configuration options for MySQL MCP connection.
/// </summary>
public class MySqlOptions
{
    /// <summary>
    /// Database server hostname or IP address.
    /// </summary>
    public string Host { get; set; } = "localhost";
    
    /// <summary>
    /// MySQL server port. Default: 3306
    /// </summary>
    public int Port { get; set; } = 3306;
    
    /// <summary>
    /// Database name to connect to.
    /// </summary>
    public string Database { get; set; } = "";
    
    /// <summary>
    /// MySQL username.
    /// </summary>
    public string User { get; set; } = "";
    
    /// <summary>
    /// MySQL password.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Home environment preset (localhost:3308 with sysadm user).
    /// </summary>
    public static MySqlOptions Home => new()
    {
        Host = "localhost",
        Port = 3308,
        Database = "san",
        User = "sysadm",
        Password = "john"
    };

    /// <summary>
    /// Creates options from environment variables.
    /// Reads: MYSQL_HOST, MYSQL_PORT, MYSQL_DATABASE, MYSQL_USER, MYSQL_PASSWORD
    /// </summary>
    public static MySqlOptions FromEnvironment() => new()
    {
        Host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost",
        Port = int.TryParse(Environment.GetEnvironmentVariable("MYSQL_PORT"), out int port) ? port : 3306,
        Database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "",
        User = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "",
        Password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? ""
    };
}

