using System.Text;

namespace SmarterViews.Desktop.Services;

/// <summary>
/// Helper class to build connection strings for various database types
/// </summary>
public class ConnectionStringBuilder
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IntegratedSecurity { get; set; }
    public string FilePath { get; set; } = string.Empty; // For SQLite
    public Dictionary<string, string> AdditionalParameters { get; set; } = new();

    /// <summary>
    /// Builds a connection string based on the database type
    /// </summary>
    public string Build(string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => BuildSqlServerConnectionString(),
            "MySql" => BuildMySqlConnectionString(),
            "PostgreSQL" => BuildPostgreSqlConnectionString(),
            "SQLite" => BuildSQLiteConnectionString(),
            "Oracle" => BuildOracleConnectionString(),
            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Parses an existing connection string and populates the builder properties
    /// </summary>
    public void Parse(string connectionString, string databaseType)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length != 2) continue;

            var key = keyValue[0].Trim().ToLowerInvariant();
            var value = keyValue[1].Trim();

            switch (key)
            {
                case "server":
                case "host":
                case "data source":
                    if (databaseType == "SQLite")
                        FilePath = value;
                    else
                        Server = value;
                    break;
                case "database":
                case "initial catalog":
                    Database = value;
                    break;
                case "user id":
                case "uid":
                case "username":
                case "user":
                    Username = value;
                    break;
                case "password":
                case "pwd":
                    Password = value;
                    break;
                case "port":
                    if (int.TryParse(value, out int port))
                        Port = port;
                    break;
                case "integrated security":
                case "trusted_connection":
                    IntegratedSecurity = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                        value.Equals("sspi", StringComparison.OrdinalIgnoreCase) ||
                                        value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }
    }

    private string BuildSqlServerConnectionString()
    {
        var builder = new StringBuilder();
        builder.Append($"Server={Server}");
        
        if (!string.IsNullOrWhiteSpace(Database))
            builder.Append($";Database={Database}");

        if (IntegratedSecurity)
        {
            builder.Append(";Integrated Security=True");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(Username))
                builder.Append($";User Id={Username}");
            if (!string.IsNullOrWhiteSpace(Password))
                builder.Append($";Password={Password}");
        }

        builder.Append(";TrustServerCertificate=True");
        AppendAdditionalParameters(builder);
        
        return builder.ToString();
    }

    private string BuildMySqlConnectionString()
    {
        var builder = new StringBuilder();
        builder.Append($"Server={Server}");
        
        if (Port > 0)
            builder.Append($";Port={Port}");
        else
            builder.Append(";Port=3306");

        if (!string.IsNullOrWhiteSpace(Database))
            builder.Append($";Database={Database}");
        
        if (!string.IsNullOrWhiteSpace(Username))
            builder.Append($";Uid={Username}");
        
        if (!string.IsNullOrWhiteSpace(Password))
            builder.Append($";Pwd={Password}");

        AppendAdditionalParameters(builder);
        
        return builder.ToString();
    }

    private string BuildPostgreSqlConnectionString()
    {
        var builder = new StringBuilder();
        builder.Append($"Host={Server}");
        
        if (Port > 0)
            builder.Append($";Port={Port}");
        else
            builder.Append(";Port=5432");

        if (!string.IsNullOrWhiteSpace(Database))
            builder.Append($";Database={Database}");
        
        if (!string.IsNullOrWhiteSpace(Username))
            builder.Append($";Username={Username}");
        
        if (!string.IsNullOrWhiteSpace(Password))
            builder.Append($";Password={Password}");

        AppendAdditionalParameters(builder);
        
        return builder.ToString();
    }

    private string BuildSQLiteConnectionString()
    {
        var builder = new StringBuilder();
        
        if (!string.IsNullOrWhiteSpace(FilePath))
            builder.Append($"Data Source={FilePath}");
        else if (!string.IsNullOrWhiteSpace(Database))
            builder.Append($"Data Source={Database}");
        
        AppendAdditionalParameters(builder);
        
        return builder.ToString();
    }

    private string BuildOracleConnectionString()
    {
        var builder = new StringBuilder();
        
        if (Port > 0)
            builder.Append($"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Server})(PORT={Port}))(CONNECT_DATA=(SERVICE_NAME={Database})))");
        else
            builder.Append($"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Server})(PORT=1521))(CONNECT_DATA=(SERVICE_NAME={Database})))");

        if (!string.IsNullOrWhiteSpace(Username))
            builder.Append($";User Id={Username}");
        
        if (!string.IsNullOrWhiteSpace(Password))
            builder.Append($";Password={Password}");

        AppendAdditionalParameters(builder);
        
        return builder.ToString();
    }

    private void AppendAdditionalParameters(StringBuilder builder)
    {
        foreach (var param in AdditionalParameters)
        {
            if (!string.IsNullOrWhiteSpace(param.Value))
                builder.Append($";{param.Key}={param.Value}");
        }
    }

    /// <summary>
    /// Gets default port for a database type
    /// </summary>
    public static int GetDefaultPort(string databaseType)
    {
        return databaseType switch
        {
            "MySql" => 3306,
            "PostgreSQL" => 5432,
            "Oracle" => 1521,
            "SqlServer" => 1433,
            _ => 0
        };
    }
}
