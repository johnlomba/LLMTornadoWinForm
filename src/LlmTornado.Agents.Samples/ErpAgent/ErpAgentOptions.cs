using LlmTornado.Mcp;

namespace LlmTornado.Agents.Samples.ErpAgent;

/// <summary>
/// Database provider selection for ERP agent.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Microsoft SQL Server (uses mssql_mcp_server)
    /// </summary>
    MsSql,
    
    /// <summary>
    /// MySQL (uses mysql-mcp-server)
    /// </summary>
    MySql
}

/// <summary>
/// Configuration options for the ERP orchestration agent.
/// Supports switching between MSSQL (work) and MySQL (home) environments.
/// </summary>
public class ErpAgentOptions
{
    /// <summary>
    /// Which database provider to use.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.MsSql;
    
    /// <summary>
    /// MSSQL connection options (required when Provider = MsSql).
    /// </summary>
    public MsSqlOptions? MsSql { get; set; }
    
    /// <summary>
    /// MySQL connection options (required when Provider = MySql).
    /// </summary>
    public MySqlOptions? MySql { get; set; }

    /// <summary>
    /// Maximum number of correction attempts before giving up.
    /// </summary>
    public int MaxCorrectionAttempts { get; set; } = 3;

    /// <summary>
    /// Work environment preset: MSSQL on VISUAL01/SAN.
    /// </summary>
    public static ErpAgentOptions ForWork() => new()
    {
        Provider = DatabaseProvider.MsSql,
        MsSql = MsSqlOptions.Work
    };

    /// <summary>
    /// Home environment preset: MySQL on localhost:3308.
    /// </summary>
    public static ErpAgentOptions ForHome() => new()
    {
        Provider = DatabaseProvider.MySql,
        MySql = MySqlOptions.Home
    };

    /// <summary>
    /// Auto-detect environment from ERP_LOCATION environment variable.
    /// Values: "work" (MSSQL) or "home" (MySQL). Defaults to work.
    /// </summary>
    public static ErpAgentOptions FromEnvironment()
    {
        string? location = Environment.GetEnvironmentVariable("ERP_LOCATION");
        return location?.ToLower() switch
        {
            "work" => ForWork(),
            "home" => ForHome(),
            _ => ForWork() // default to work environment
        };
    }
}

