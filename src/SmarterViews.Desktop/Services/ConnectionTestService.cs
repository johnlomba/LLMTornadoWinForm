using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Service for testing database connections
/// </summary>
public class ConnectionTestService
{
    /// <summary>
    /// Tests a database connection and returns the result
    /// </summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, string databaseType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = CreateConnection(connectionString, databaseType);
            
            var startTime = DateTime.UtcNow;
            await connection.OpenAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            // Get server version if available
            var serverInfo = GetServerInfo(connection, databaseType);

            return new ConnectionTestResult
            {
                Success = true,
                Message = $"Connection successful!\n\nServer: {serverInfo}\nConnection time: {duration.TotalMilliseconds:F0}ms",
                Duration = duration
            };
        }
        catch (SqlException ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SQL Server connection failed:\n{ex.Message}",
                ErrorDetails = GetSqlServerErrorDetails(ex)
            };
        }
        catch (MySqlException ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"MySQL connection failed:\n{ex.Message}",
                ErrorDetails = $"Error Code: {ex.Number}"
            };
        }
        catch (NpgsqlException ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"PostgreSQL connection failed:\n{ex.Message}",
                ErrorDetails = ex.SqlState
            };
        }
        catch (SqliteException ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"SQLite connection failed:\n{ex.Message}",
                ErrorDetails = $"SQLite Error Code: {ex.SqliteErrorCode}"
            };
        }
        catch (OracleException ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Oracle connection failed:\n{ex.Message}",
                ErrorDetails = $"Error Number: {ex.Number}"
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection failed:\n{ex.Message}",
                ErrorDetails = ex.GetType().Name
            };
        }
    }

    private DbConnection CreateConnection(string connectionString, string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => new SqlConnection(connectionString),
            "MySql" => new MySqlConnection(connectionString),
            "PostgreSQL" => new NpgsqlConnection(connectionString),
            "SQLite" => new SqliteConnection(connectionString),
            "Oracle" => new OracleConnection(connectionString),
            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    private string GetServerInfo(DbConnection connection, string databaseType)
    {
        try
        {
            return databaseType switch
            {
                "SqlServer" => $"SQL Server {connection.ServerVersion} - Database: {connection.Database}",
                "MySql" => $"MySQL {connection.ServerVersion} - Database: {connection.Database}",
                "PostgreSQL" => $"PostgreSQL {connection.ServerVersion} - Database: {connection.Database}",
                "SQLite" => $"SQLite {connection.ServerVersion} - Database: {connection.Database}",
                "Oracle" => $"Oracle {connection.ServerVersion}",
                _ => connection.ServerVersion
            };
        }
        catch
        {
            return "Connected (version info unavailable)";
        }
    }

    private string GetSqlServerErrorDetails(SqlException ex)
    {
        var details = $"Error Number: {ex.Number}\n";
        
        switch (ex.Number)
        {
            case 4060:
                details += "Hint: The database name might be incorrect or the database doesn't exist.";
                break;
            case 18456:
                details += "Hint: Login failed. Check username and password.";
                break;
            case 53:
            case -1:
                details += "Hint: Cannot connect to server. Check server name and network connectivity.";
                break;
            case 2:
                details += "Hint: Server not found or not accessible. Check server name.";
                break;
            default:
                details += "Check the error message above for details.";
                break;
        }

        return details;
    }
}

/// <summary>
/// Result of a connection test
/// </summary>
public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public TimeSpan Duration { get; set; }
}
